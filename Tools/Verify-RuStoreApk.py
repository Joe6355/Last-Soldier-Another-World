"""Inspect a built APK; this does not replace testing on an Android device."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess
import zipfile


def command(arguments):
    result = subprocess.run([str(item) for item in arguments], capture_output=True, encoding="utf-8", errors="replace")
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip())
    return result.stdout


def inspect(apk, build_tools, java_home):
    badging = command([build_tools / "aapt.exe", "dump", "badging", apk])
    manifest = command([build_tools / "aapt.exe", "dump", "xmltree", apk, "AndroidManifest.xml"])
    certificate = command([java_home / "bin/java.exe", "-jar", build_tools / "lib/apksigner.jar",
                           "verify", "--verbose", "--print-certs", apk])
    with apk.open("rb") as apk_stream:
        apk_hash = hashlib.file_digest(apk_stream, "sha256").hexdigest()
    report = {"apk": apk.name, "bytes": apk.stat().st_size,
              "sha256": apk_hash, "checks": [], "arm64Libraries": []}

    def check(name, passed):
        report["checks"].append({"name": name, "passed": bool(passed)})

    package = re.search(r"package: name='([^']+)' versionCode='([^']+)' versionName='([^']+)'", badging)
    report["package"], report["versionCode"], report["versionName"] = package.groups()
    report["minSdk"] = int(re.search(r"sdkVersion:'(\d+)'", badging)[1])
    report["targetSdk"] = int(re.search(r"targetSdkVersion:'(\d+)'", badging)[1])
    report["permissions"] = sorted(set(re.findall(r"uses-permission(?:-sdk-\d+)?: name='([^']+)'", badging)))
    report["certificateSha256"] = re.search(r"Signer #1 certificate SHA-256 digest: (\w+)", certificate)[1]
    report["signatureVerification"] = [line.strip() for line in certificate.splitlines() if line.startswith("Verified using")]
    check("Dedicated RuStore package", report["package"] == "com.joe6355.lastsoldier.rustore")
    check("Version code is positive", int(report["versionCode"]) > 0)
    check("Android API 26 / target 35", report["minSdk"] == 26 and report["targetSdk"] == 35)
    check("Non-debuggable", "application-debuggable" not in badging and not re.search(r"android:debuggable.*(?:0xffffffff|true)", manifest))
    check("Custom release certificate", "Android Debug" not in certificate and "CN=Android Debug" not in certificate)
    check("APK v2 signature", "Verified using v2 scheme (APK Signature Scheme v2): true" in certificate)
    check("Advertising ID permission excluded", "com.google.android.gms.permission.AD_ID" not in report["permissions"])
    check("No dangerous location/storage/microphone/camera permissions", not any(
        permission in report["permissions"] for permission in ["android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION", "android.permission.READ_EXTERNAL_STORAGE",
        "android.permission.WRITE_EXTERNAL_STORAGE", "android.permission.RECORD_AUDIO", "android.permission.CAMERA"]))
    check("Backup disabled", bool(re.search(r"android:allowBackup.*=\(type 0x12\)0x0", manifest)))
    check("Cleartext disabled", bool(re.search(r"android:usesCleartextTraffic.*=\(type 0x12\)0x0", manifest)))
    check("No sign-in activity in the local-only release", "com.joe6355.lastsoldier.rustore.YandexAuthActivity" not in manifest)
    with zipfile.ZipFile(apk) as archive, apk.open("rb") as raw:
        check("ZIP CRC integrity", archive.testzip() is None)
        dex = b"".join(archive.read(name) for name in archive.namelist() if name.endswith(".dex"))
        check("LoginSDK and native sign-in bridge excluded", all(name not in dex for name in [
            b"Lcom/yandex/authsdk/YandexAuthSdk;", b"Lcom/joe6355/lastsoldier/rustore/NativeBridge;",
            b"Lcom/joe6355/lastsoldier/rustore/YandexAuthActivity;"]))
        for info in archive.infolist():
            if not info.filename.startswith("lib/arm64-v8a/") or not info.filename.endswith(".so"):
                continue
            data = archive.read(info)
            if data[:6] != b"\x7fELF\x02\x01":
                raise ValueError("Expected little-endian ELF64: " + info.filename)
            offset = struct.unpack_from("<Q", data, 32)[0]
            size, count = struct.unpack_from("<HH", data, 54)
            alignments = [struct.unpack_from("<Q", data, offset + index * size + 48)[0]
                          for index in range(count) if struct.unpack_from("<I", data, offset + index * size)[0] == 1]
            raw.seek(info.header_offset + 26)
            name_bytes, extra_bytes = struct.unpack("<HH", raw.read(4))
            data_offset = info.header_offset + 30 + name_bytes + extra_bytes
            # Compressed libraries are extracted before loading. Uncompressed
            # libraries must additionally be aligned inside the APK itself.
            zip_aligned = info.compress_type != zipfile.ZIP_STORED or data_offset % 16384 == 0
            entry = {"name": info.filename, "loadAlignments": alignments,
                     "compressed": info.compress_type != zipfile.ZIP_STORED,
                     "zipDataOffset": data_offset, "zip16KiBAligned": zip_aligned}
            report["arm64Libraries"].append(entry)
            check(info.filename + " ELF / ZIP 16 KiB", bool(alignments) and all(value >= 16384 for value in alignments) and zip_aligned)
        check("ARM64 native code exists", len(report["arm64Libraries"]) > 0)
        engine = archive.read("lib/arm64-v8a/libunity.so")
        check("Patched Unity 2022.3.62f2", b"2022.3.62f2" in engine)
    report["result"] = "PASS" if all(item["passed"] for item in report["checks"]) else "FAIL"
    report["limitations"] = ["Static APK inspection only", "No real-device or live advertising verification", "Local saves; sign-in and shared leaderboard excluded"]
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("apk", type=Path)
    parser.add_argument("--build-tools", type=Path, required=True)
    parser.add_argument("--java-home", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    arguments = parser.parse_args()
    result = inspect(arguments.apk.resolve(), arguments.build_tools.resolve(), arguments.java_home.resolve())
    arguments.out.parent.mkdir(parents=True, exist_ok=True)
    arguments.out.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{result['result']}: {len(result['checks'])} APK checks; report: {arguments.out}")
    for item in result["checks"]:
        if not item["passed"]:
            print("FAIL:", item["name"])
    raise SystemExit(0 if result["result"] == "PASS" else 1)
