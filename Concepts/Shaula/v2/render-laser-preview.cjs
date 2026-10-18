// Offscreen render of our own Canvas code; does not automate or open a browser.
const fs=require('node:fs');
const path=require('node:path');
const vm=require('node:vm');
const assert=require('node:assert/strict');
const {createRequire}=require('node:module');
const bundle=createRequire('C:/Users/79870/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/shaula.cjs');
const {createCanvas,ImageData,GlobalFonts}=bundle('@napi-rs/canvas');
GlobalFonts.registerFromPath('C:/Windows/Fonts/arial.ttf','Arial');
const sharp=bundle('sharp');
const root=__dirname;
const manifest=JSON.parse(fs.readFileSync(path.join(root,'animations.json'),'utf8'));
const canvas=createCanvas(960,520),ctx=canvas.getContext('2d');
const elements={arena:canvas};
for(const id of ['action','phase','play','restart','frame','counter','time','status'])
  elements[id]={value:id==='action'?'infernal-shot':id==='phase'?'normal':'0',textContent:'',addEventListener(){}};
const win={SHAULA_MANIFEST:manifest,matchMedia(){return {matches:false};},
  async SHAULA_ASSET_LOADER(file){
    const {data,info}=await sharp(fs.readFileSync(path.join(root,file))).ensureAlpha().raw().toBuffer({resolveWithObject:true});
    const picture=createCanvas(info.width,info.height);
    picture.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(data.buffer,data.byteOffset,data.length),info.width,info.height),0,0);
    return picture;
  }};
const environment=vm.createContext({window:win,document:{getElementById(id){assert.ok(elements[id],id);return elements[id];}},
  requestAnimationFrame(){},console,Math,Promise,Error});
vm.runInContext(fs.readFileSync(path.join(root,'preview-logic.js'),'utf8'),environment,{filename:'preview-logic.js'});
vm.runInContext(fs.readFileSync(path.join(root,'preview.js'),'utf8'),environment,{filename:'preview.js'});
const framesDir=path.join(root,'laser-preview-frames');
fs.mkdirSync(framesDir,{recursive:true});
async function main(){
  const deadline=Date.now()+8000;
  while(!win.ShaulaPreview?.ready){if(Date.now()>deadline)throw new Error(elements.status.textContent||'Image load timeout');await new Promise(resolve=>setTimeout(resolve,20));}
  const render=(phase,time)=>{win.ShaulaPreview.select('infernal-shot',phase);win.ShaulaPreview.seek(time);};
  const results=[];
  for(const phase of ['normal','crimson']){
    render(phase,1520);
    const pixels=ctx.getImageData(411,250,92,62).data;
    let hot=0;
    for(let i=0;i<pixels.length;i+=4)if(pixels[i]>=245&&pixels[i+1]>=245&&pixels[i+2]>=245)hot++;
    assert.ok(hot>150,phase+': white laser core must be visible between boss and target');
    fs.writeFileSync(path.join(root,'shaula-white-laser-peak-'+phase+'.png'),canvas.toBuffer('image/png'));
    render(phase,1490);
    const before=ctx.getImageData(411,250,92,62).data;
    let beforeHot=0;for(let i=0;i<before.length;i+=4)if(before[i]>=245&&before[i+1]>=245&&before[i+2]>=245)beforeHot++;
    assert.ok(beforeHot<hot/5,phase+': no laser before the 1.5-second telegraph completes');
    render(phase,1740);
    const after=ctx.getImageData(411,250,92,62).data;
    let afterHot=0;for(let i=0;i<after.length;i+=4)if(after[i]>=245&&after[i+1]>=245&&after[i+2]>=245)afterHot++;
    assert.ok(afterHot<hot/5,phase+': the main beam has faded after 180ms');
    results.push({phase,whiteCorePixels:hot,beforeChargeComplete:beforeHot,afterBeam:afterHot});
  }
  for(const phase of ['normal','crimson'])for(const action of [...Object.keys(manifest.clips),'chase','armor']){
    win.ShaulaPreview.select(action,phase);
    const state=win.ShaulaPreviewLogic.resolve(manifest,action,phase,0);
    for(const t of [0,state.duration*.5,state.duration-1])win.ShaulaPreview.seek(t);
    if(['tail-break','tail-regrow','enrage','defeat'].includes(action))
      fs.writeFileSync(path.join(root,'shaula-'+action+'-'+phase+'.png'),canvas.toBuffer('image/png'));
  }
  let number=0;
  for(const phase of ['normal','crimson'])for(let t=0;t<=2440;t+=40){
    render(phase,Math.min(t,manifest.clips['infernal-shot'].totalMs-1));
    fs.writeFileSync(path.join(framesDir,String(number++).padStart(3,'0')+'.png'),canvas.toBuffer('image/png'));
  }
  const report={result:'PASS',renderer:'@napi-rs/canvas; shared preview.js, no browser session',
    laserPhases:results,renderedActionPhaseCombinations:28,renderedFrames:number,frameMs:40,
    browserInteractionVerified:false,gameIntegrated:false};
  fs.writeFileSync(path.join(root,'laser-render-checks.json'),JSON.stringify(report,null,2)+'\n');
  console.log(JSON.stringify(report,null,2));
}
main().catch(error=>{console.error(error);process.exitCode=1;});
