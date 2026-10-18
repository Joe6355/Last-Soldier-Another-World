"""Losslessly slice generated frames and encode animation containers, outside Unity."""
from pathlib import Path
import hashlib
import json
import math
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
COLS = ROWS = 6
MAIN = ['idle', 'walk', 'grab', 'slam', 'infernal-shot', 'piercing-sting']
SPECIAL = ['star-barrage', 'needle-volley', 'tail-break', 'tail-regrow', 'enrage', 'defeat']
LABELS = ['Ожидание', 'Ходьба', 'Смертельная хватка', 'Сокрушительный удар', 'Адский выстрел',
          'Пронзающее жало', 'Звёздный обстрел', 'Шквал жал', 'Разрушение хвоста',
          'Регенерация жала', 'Багровая ярость', 'Последнее превращение']
DURATIONS = [[180]*6, [110]*6, [260,260,110,130,320,650], [400,400,100,120,320,400],
             [600,600,300,90,180,320], [500,500,100,180,1000,320],
             [300,350,160,160,160,450], [300,140,140,140,140,450],
             [250,180,180,200,300,700], [300,240,220,220,240,600],
             [350,260,260,260,300,650], [400,300,250,250,500,1000]]

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    sheets = {}
    images = {}
    for key in ['normal', 'crimson', 'special-normal', 'special-crimson']:
        name = 'shaula-' + key + '-sheet.png'
        path = ROOT / name
        im = Image.open(path).convert('RGBA')
        assert im.width % COLS == 0 and im.height % ROWS == 0, (name, im.size)
        assert im.width == im.height, (name, im.size)
        alpha = im.getchannel('A')
        assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] >= 250
        sheets[key] = dict(file=name, width=im.width, height=im.height,
                           cell=im.width//COLS, columns=COLS, rows=ROWS,
                           sha256=sha(path), transparentPixels=alpha.histogram()[0])
        images[key] = im
    laser_path=ROOT/'shaula-white-laser-sheet.png'
    laser=Image.open(laser_path).convert('RGBA')
    assert laser.width%6==0 and laser.height%2==0 and laser.width//6==laser.height//2
    sheets['laser']=dict(file=laser_path.name,width=laser.width,height=laser.height,
                         cell=laser.width//6,cellHeight=laser.height//2,columns=6,rows=2,
                         sha256=sha(laser_path),transparentPixels=laser.getchannel('A').histogram()[0])
    images['laser']=laser
    frame_size = math.ceil((max(item['cell'] for item in sheets.values()) + 64)/32)*32
    clips = {}
    saved_frames = {}
    for index, name in enumerate(MAIN + SPECIAL):
        clips[name] = dict(label=LABELS[index], row=index%6, kind='main' if index<6 else 'special',
                           durationsMs=DURATIONS[index], loop=name in ['idle','walk'],
                           totalMs=sum(DURATIONS[index]), frameCount=6)
        if name == 'defeat':
            clips[name]['previewFrameScales'] = [1,1,1,1,.6,.45]
        if name == 'infernal-shot':
            clips[name].update(kind='laser',row=0,phaseRows=dict(normal=0,crimson=1),
                               muzzleAnchors=[[.645,.156],[.516,.269],[.512,.251],[.573,.298],[.496,.291],[.698,.158]],
                               beamColor='#ffffff',beamDurationMs=180,impactDurationMs=450,
                               previousMainAtlasRowUnused=4)
        for phase in ['normal', 'crimson']:
            key = phase if index<6 else 'special-'+phase
            if name == 'infernal-shot': key='laser'
            im = images[key]; cell = sheets[key]['cell']; row = index%6
            if name == 'infernal-shot': row=0 if phase=='normal' else 1
            frames = []
            folder = ROOT/'frames'/phase/name
            folder.mkdir(parents=True,exist_ok=True)
            for column in range(6):
                raw = im.crop((column*cell,row*cell,(column+1)*cell,(row+1)*cell))
                assert raw.getchannel('A').getbbox() is not None
                frame = Image.new('RGBA',(frame_size,frame_size))
                offset = (frame_size-cell)//2
                frame.paste(raw,(offset,offset))
                # Exporting/adding empty padding does not recolor any art pixel.
                assert frame.crop((offset,offset,offset+cell,offset+cell)).tobytes() == raw.tobytes()
                frame.save(folder/f'{column:02}.png')
                frames.append(frame)
            target=ROOT/'animations'/phase
            target.mkdir(parents=True,exist_ok=True)
            frames[0].save(target/(name+'.apng'),format='PNG',save_all=True,append_images=frames[1:],
                           duration=DURATIONS[index],loop=0 if clips[name]['loop'] else 1,disposal=0,blend=0,optimize=False)
            with Image.open(target/(name+'.apng')) as check:
                assert check.n_frames == 6
            saved_frames[(phase,name)] = frames
    # Reference crop uses the existing Unity sprite rectangle, not a new hero.
    hero_sheet=Image.open(ROOT/'hero-reference-sheet.png').convert('RGBA')
    hero=hero_sheet.crop((4,hero_sheet.height-31,26,hero_sheet.height))
    hero.save(ROOT/'hero-reference.png')
    hero_box=hero.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    first=images['normal'].crop((0,0,sheets['normal']['cell'],sheets['normal']['cell']))
    boss_box=first.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    first.save(ROOT/'shaula-black.png')
    images['crimson'].crop((0,0,sheets['crimson']['cell'],sheets['crimson']['cell'])).save(ROOT/'shaula-crimson.png')
    for name,row,column in [('shaula-tailless',2,5),('shaula-small',5,5)]:
        im=images['special-crimson']; cell=sheets['special-crimson']['cell']
        im.crop((column*cell,row*cell,(column+1)*cell,(row+1)*cell)).save(ROOT/(name+'.png'))
    manifest = dict(conceptOnly=True, integratedIntoGame=False, columns=6, rows=6, frameSize=frame_size,
                    view='three-quarter top-down facing down-left', sheets=sheets, clips=clips,
                    hero=dict(file='hero-reference.png',opaqueBounds=hero_box),
                    scale=dict(bossWidthInHeroHeights=6,referenceBossBounds=boss_box,
                               note='Presentation scale only; no Unity object scale has been changed'),
                    macros=dict(chase=dict(clip='grab',repeats=3,recoveryMs=900),
                                armor=dict(clip='idle',weakPointHighlight=True)),
                    timings=dict(infernalShotChargeMs=1500,stingTelegraphMs=1000,whiteLaserDurationMs=180,
                                 proposedAimLockBeforeShotMs=400,otherTimingsProvisional=True),
                    limitation='AI animation draft: one angle; frame edge residue and final pivot cleanup need review before production import')
    (ROOT/'animations.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n','utf-8')
    template=(ROOT/'preview-template.html').read_text('utf-8')
    (ROOT/'preview.html').write_text(template.replace('__SHAULA_MANIFEST__',json.dumps(manifest,ensure_ascii=False)),encoding='utf-8')
    # A contact reel renders the supplied frames; it does not synthesize new poses.
    font_path=Path('C:/Windows/Fonts/arial.ttf')
    font=ImageFont.truetype(str(font_path),22)
    smallfont=ImageFont.truetype(str(font_path),17)
    hero_opaque_height=hero_box[3]-hero_box[1]
    hero_scale=40/hero_opaque_height
    hero_preview=hero.resize((round(hero.width*hero_scale),round(hero.height*hero_scale)),Image.Resampling.NEAREST)
    boss_scale=240/(boss_box[2]-boss_box[0])
    reel=[]; timing=[]
    for name in ['walk','grab','slam','infernal-shot','piercing-sting','star-barrage','needle-volley','tail-break','tail-regrow','enrage','defeat']:
        clip=clips[name]
        for f in range(6):
            canvas=Image.new('RGB',(880,390),'#19352e')
            draw=ImageDraw.Draw(canvas)
            draw.text((22,18),'ШАУЛА · '+clip['label'],font=font,fill='#f4edd7')
            draw.text((22,49),'Анимационный концепт · ширина босса = 6 высот героя',font=smallfont,fill='#bacbbd')
            for phase,x in [('normal',260),('crimson',655)]:
                frame=saved_frames[(phase,name)][f]
                factor=boss_scale
                if name=='infernal-shot': factor*=sheets['normal']['cell']/sheets['laser']['cell']
                if name=='defeat' and f>=4:
                    factor*=.6 if f==4 else .45
                scaled=frame.resize((round(frame.width*factor),round(frame.height*factor)),Image.Resampling.NEAREST)
                at=(round(x-scaled.width/2),round(230-scaled.height*.55))
                canvas.paste(scaled,at,scaled)
                canvas.paste(hero_preview,(x-170,round(286-hero_preview.height)),hero_preview)
                draw.text((x-92,355),'Чёрный панцирь' if phase=='normal' else 'Багровая форма',font=smallfont,fill='#f4edd7')
            reel.append(canvas)
            timing.append(clip['durationsMs'][f])
    reel[0].save(ROOT/'shaula-motion-preview.gif',save_all=True,append_images=reel[1:],
                 duration=timing,loop=0,disposal=2,optimize=False)
    print(json.dumps(dict(sheets=len(sheets),generatedFrames=4*36+12,activeFrames=4*36,animatedClips=24,
                          paddedFrameSize=frame_size,reelFrames=len(reel),result='PASS'),indent=2))

if __name__=='__main__':
    main()
