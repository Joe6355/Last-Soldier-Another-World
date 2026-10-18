"""Losslessly slice generated frames and encode animation containers, outside Unity."""
from pathlib import Path
import hashlib
import json
import math
import html
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

def grid_rects(width,height,columns,rows):
    xs=[round(width*i/columns) for i in range(columns+1)]
    ys=[round(height*i/rows) for i in range(rows+1)]
    return [[[xs[c],ys[r],xs[c+1]-xs[c],ys[r+1]-ys[r]] for c in range(columns)] for r in range(rows)]

def crop_frame(image,sheet,row,column):
    x,y,w,h=sheet['rects'][row][column]
    return image.crop((x,y,x+w,y+h))

def main():
    sheets = {}
    images = {}
    for key in ['normal', 'crimson', 'special-normal', 'special-crimson']:
        name = 'shaula-' + key + '-sheet.png'
        path = ROOT / name
        im = Image.open(path).convert('RGBA')
        assert im.width == im.height, (name, im.size)
        alpha = im.getchannel('A')
        assert alpha.getextrema()[0] == 0 and alpha.getextrema()[1] >= 250
        sheets[key] = dict(file=name, width=im.width, height=im.height,
                           cell=im.width/COLS,cellHeight=im.height/ROWS, columns=COLS, rows=ROWS,
                           rects=grid_rects(im.width,im.height,COLS,ROWS),
                           sha256=sha(path), transparentPixels=alpha.histogram()[0])
        images[key] = im
    laser_path=ROOT/'shaula-white-laser-sheet.png'
    laser=Image.open(laser_path).convert('RGBA')
    assert abs(laser.width/6-laser.height/2)<2,(laser.size,'Expected six columns and two rows')
    sheets['laser']=dict(file=laser_path.name,width=laser.width,height=laser.height,
                         cell=laser.width/6,cellHeight=laser.height/2,columns=6,rows=2,
                         rects=grid_rects(laser.width,laser.height,6,2),
                         sha256=sha(laser_path),transparentPixels=laser.getchannel('A').histogram()[0])
    images['laser']=laser
    frame_size = math.ceil((max(item['cell'] for item in sheets.values()) + 64)/32)*32
    tail_path=ROOT/'shaula-tailless-sheet.png'
    tail=Image.open(tail_path).convert('RGBA')
    sheets['tailless']=dict(file=tail_path.name,width=tail.width,height=tail.height,
                            cell=tail.width/2,cellHeight=tail.height,columns=2,rows=1,
                            rects=grid_rects(tail.width,tail.height,2,1),sha256=sha(tail_path),
                            transparentPixels=tail.getchannel('A').histogram()[0])
    images['tailless']=tail
    normal_box=crop_frame(images['normal'],sheets['normal'],0,0).getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    tail_box=crop_frame(tail,sheets['tailless'],0,0).getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    correction=(normal_box[2]-normal_box[0])/sheets['normal']['cell']/((tail_box[2]-tail_box[0])/sheets['tailless']['cell'])
    sheets['tailless']['scaleCorrection']=correction
    sheets['tailless']['pivot']=[.53,tail_box[3]/sheets['tailless']['cellHeight']-(normal_box[3]/sheets['normal']['cell']-.77)/correction]
    clips = {}
    saved_frames = {}
    for index, name in enumerate(MAIN + SPECIAL):
        clips[name] = dict(label=LABELS[index], row=index%6, kind='main' if index<6 else 'special',
                           durationsMs=DURATIONS[index], loop=name in ['idle','walk'],
                           totalMs=sum(DURATIONS[index]), frameCount=6)
        if name == 'defeat':
            clips[name]['previewFrameScales'] = [1,1,1,1,.6,.45]
            clips[name]['frameInsetTop']={'4':.17,'5':.17}
        if name in ['tail-break','tail-regrow']:
            clips[name]['frameOverrides']={}
            for phase,column in [('normal',0),('crimson',1)]:
                mapping={}
                for f in range(6):
                    absent=(f>=3 if name=='tail-break' else f==0)
                    mapping[str(f)]=dict(sheet='tailless' if absent else phase,row=0,column=column if absent else 0)
                clips[name]['frameOverrides'][phase]=mapping
            clips[name]['frameInsetTop']=({'1':.12,'2':.3} if name=='tail-break' else {'1':.4,'2':.3,'3':.2,'4':.08})
        clip_size=math.ceil((max(tail.width/2,tail.height)+64)/32)*32 if name in ['tail-break','tail-regrow'] else frame_size
        clips[name]['frameCanvasSize']=clip_size
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
                override=clips[name].get('frameOverrides',{}).get(phase,{}).get(str(column))
                raw = crop_frame(images[override['sheet']],sheets[override['sheet']],override['row'],override['column']) if override else crop_frame(im,sheets[key],row,column)
                assert raw.getchannel('A').getbbox() is not None
                frame = Image.new('RGBA',(clip_size,clip_size))
                ox,oy=(clip_size-raw.width)//2,(clip_size-raw.height)//2
                trim=round(raw.height*clips[name].get('frameInsetTop',{}).get(str(column),0))
                if trim:
                    raw=raw.crop((0,trim,raw.width,raw.height))
                    oy+=trim
                frame.paste(raw,(ox,oy))
                # Exporting/adding empty padding does not recolor any art pixel.
                assert frame.crop((ox,oy,ox+raw.width,oy+raw.height)).tobytes() == raw.tobytes()
                frame.save(folder/f'{column:02}.png')
                frames.append(frame)
            target=ROOT/'animations'/phase
            target.mkdir(parents=True,exist_ok=True)
            frames[0].save(target/(name+'.apng'),format='PNG',save_all=True,append_images=frames[1:],
                           duration=DURATIONS[index],loop=0 if clips[name]['loop'] else 1,disposal=0,blend=0,optimize=False)
            with Image.open(target/(name+'.apng')) as check:
                assert 1<=check.n_frames<=6
            saved_frames[(phase,name)] = frames
    # Reference crop uses the existing Unity sprite rectangle, not a new hero.
    hero_sheet=Image.open(ROOT/'hero-reference-sheet.png').convert('RGBA')
    hero=hero_sheet.crop((4,hero_sheet.height-31,26,hero_sheet.height))
    hero.save(ROOT/'hero-reference.png')
    hero_box=hero.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    first=crop_frame(images['normal'],sheets['normal'],0,0)
    boss_box=first.getchannel('A').point(lambda a:255 if a>=128 else 0).getbbox()
    first.save(ROOT/'shaula-black.png')
    crop_frame(images['crimson'],sheets['crimson'],0,0).save(ROOT/'shaula-crimson.png')
    for name,row,column in [('shaula-tailless',2,5),('shaula-small',5,5)]:
        pose=crop_frame(images['tailless'],sheets['tailless'],0,1) if name=='shaula-tailless' else crop_frame(images['special-crimson'],sheets['special-crimson'],row,column)
        pose.save(ROOT/(name+'.png'))
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
    audio=json.loads((ROOT/'audio-manifest.json').read_text('utf-8'))
    options=''.join('<option value="'+html.escape(c['file'],quote=True)+'">'+html.escape(c['label'])+'</option>' for c in audio['clips'])
    output=template.replace('__SHAULA_MANIFEST__',json.dumps(manifest,ensure_ascii=False)).replace('__SHAULA_AUDIO_OPTIONS__',options)
    (ROOT/'preview.html').write_text(output,encoding='utf-8')
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
                override=clips[name].get('frameOverrides',{}).get(phase,{}).get(str(f))
                key=override['sheet'] if override else 'laser' if name=='infernal-shot' else phase if clips[name]['kind']=='main' else 'special-'+phase
                factor*=sheets['normal']['cell']/sheets[key]['cell']*sheets[key].get('scaleCorrection',1)
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
    print(json.dumps(dict(sheets=len(sheets),generatedFrames=4*36+12+2,activeFrames=4*36,animatedClips=24,
                          paddedFrameSize=frame_size,reelFrames=len(reel),result='PASS'),indent=2))

if __name__=='__main__':
    main()
