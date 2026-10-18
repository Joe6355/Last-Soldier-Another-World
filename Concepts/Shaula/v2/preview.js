(function () {
  'use strict';
  const manifest=window.SHAULA_MANIFEST, logic=window.ShaulaPreviewLogic;
  const canvas=document.getElementById('arena'),ctx=canvas.getContext('2d');
  const action=document.getElementById('action'),phase=document.getElementById('phase');
  const play=document.getElementById('play'),restart=document.getElementById('restart');
  const scrub=document.getElementById('frame'),counter=document.getElementById('counter'),clock=document.getElementById('time'),status=document.getElementById('status');
  const reducedMotion=window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let elapsed=0,last=0,playing=!reducedMotion,ready=false,lastStatus='';
  const images={};
  function image(file){if(window.SHAULA_ASSET_LOADER)return window.SHAULA_ASSET_LOADER(file);return new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=()=>reject(new Error('Не удалось открыть '+file));im.src=file;});}
  function reset(){elapsed=0;last=0;if(action.value==='defeat')phase.value='crimson';if(action.value==='enrage')phase.value='normal';draw();}
  function line(x1,y1,x2,y2,color,width=2){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.moveTo(x1,y1);ctx.lineTo(x2,y2);ctx.stroke();}
  function ring(x,y,r,color,width=2){ctx.strokeStyle=color;ctx.lineWidth=width;ctx.beginPath();ctx.arc(x,y,r,0,Math.PI*2);ctx.stroke();}
  function spark(x,y,progress,color,count=12){for(let i=0;i<count;i++){const a=i*Math.PI*2/count;const r=8+progress*(22+(i%4)*7);ctx.globalAlpha=1-progress;ctx.fillStyle=color;ctx.fillRect(Math.round(x+Math.cos(a)*r),Math.round(y+Math.sin(a)*r),3,3);}ctx.globalAlpha=1;}
  function drawLaser(tx,ty,hx,hy,laser){
    ctx.save();ctx.globalCompositeOperation='lighter';
    if(laser.aiming){
      const strength=laser.charge*laser.charge;
      ctx.globalAlpha=.25+strength*.65;
      ring(tx,ty,6+(1-laser.charge)*14,'#e1f4ff',2);
      ctx.fillStyle='#ffffff';ctx.fillRect(tx-2-strength*3,ty-2-strength*3,4+strength*6,4+strength*6);
      line(tx-6-strength*10,ty,tx+6+strength*10,ty,'#ffffff',1+strength);
      line(tx,ty-6-strength*10,tx,ty+6+strength*10,'#ffffff',1+strength);
    }
    if(laser.firing){
      const length=Math.hypot(hx-tx,hy-ty),dx=(hx-tx)/length,dy=(hy-ty)/length;
      const endX=hx+dx*65,endY=hy+dy*65;
      ctx.globalAlpha=laser.beamAlpha*.12;line(tx,ty,endX,endY,'#b7e7ff',30);
      ctx.globalAlpha=laser.beamAlpha*.42;line(tx,ty,endX,endY,'#d4f2ff',15);
      ctx.globalAlpha=laser.beamAlpha;line(tx,ty,endX,endY,'#ffffff',8);
      const radius=12+laser.kick*17;
      line(tx-radius*1.7,ty,tx+radius*1.7,ty,'#ffffff',5);
      line(tx,ty-radius*1.7,tx,ty+radius*1.7,'#ffffff',5);
      line(tx-radius,ty-radius,tx+radius,ty+radius,'#effaff',3);
      line(tx-radius,ty+radius,tx+radius,ty-radius,'#effaff',3);
    }
    if(laser.impact>=0){
      const p=laser.impact;
      ctx.globalAlpha=1-p;
      ring(hx,hy,7+49*p,'#f4fcff',3*(1-p)+1);
      for(let i=0;i<16;i++){
        const a=i*Math.PI*2/16+.23,r=5+p*(22+(i%4)*10),tail=4+(1-p)*13;
        line(hx+Math.cos(a)*r,hy+Math.sin(a)*r,hx+Math.cos(a)*(r+tail),hy+Math.sin(a)*(r+tail),'#ffffff',i%3===0?3:2);
      }
      if(p<.3){ctx.globalAlpha=(1-p/.3)*.8;ctx.fillStyle='#ffffff';ctx.beginPath();ctx.arc(hx,hy,10*(1-p),0,Math.PI*2);ctx.fill();}
    }
    ctx.restore();
  }
  function draw(){
    if(!ready)return;
    const state=logic.resolve(manifest,action.value,phase.value,elapsed),sheet=manifest.sheets[state.sheet];
    const C=sheet.cell,CH=sheet.cellHeight||C,BB=manifest.scale.referenceBossBounds;
    const heroHeight=manifest.hero.opaqueBounds[3]-manifest.hero.opaqueBounds[1],heroScale=42/heroHeight;
    const normalCell=manifest.sheets.normal.cell;
    let scale=252/(BB[2]-BB[0])*normalCell/C*state.scale*(sheet.scaleCorrection||1);
    let bx=640,by=329,hx=236,hy=369;
    const laser=state.name==='infernal-shot'?logic.laserAt(state.t):null;
    if(laser)bx+=laser.kick*7;
    if(state.action==='chase')bx-=state.t>=state.clip.totalMs*3?192:
      state.cycle*64+Math.max(0,Math.min(1,(state.local-520)/240))*64;
    else if(state.name==='grab'&&state.frame>=2&&state.frame<4)bx-=42;
    ctx.clearRect(0,0,960,520);ctx.fillStyle='#234a39';ctx.fillRect(0,0,960,520);
    for(let y=102;y<520;y+=25)for(let x=12;x<960;x+=31){ctx.fillStyle=(x+y)%3===0?'#2a5641':'#315d48';ctx.fillRect(x+(y%3)*4,y,2,2);}
    ctx.fillStyle='#11271f';ctx.fillRect(20,18,920,66);ctx.fillStyle='#eddfb6';ctx.font='23px Arial';ctx.fillText('ШАУЛА',38,48);
    ctx.fillStyle='#4b5145';ctx.fillRect(178,36,670,13);ctx.fillStyle=phase.value==='crimson'?'#cc4f49':'#cfad62';
    ctx.fillRect(178,36,670*(state.name==='defeat'?Math.max(0,1-state.progress):phase.value==='crimson'||state.name==='enrage'?.5:1),13);
    ctx.font='15px Arial';ctx.fillStyle='#c5d2bd';ctx.fillText('20-я волна · концепт',38,70);
    ctx.save();if(laser&&!reducedMotion)ctx.translate(Math.sin(laser.age*.31)*3*laser.kick,Math.cos(laser.age*.23)*2*laser.kick);
    ctx.fillStyle='#102e25';ctx.beginPath();ctx.ellipse(bx,by+28,103*state.scale,27*state.scale,0,0,Math.PI*2);ctx.fill();
    ctx.imageSmoothingEnabled=false;
    const hero=images.hero;ctx.drawImage(hero,hx-hero.width*heroScale/2,hy-hero.height*heroScale,hero.width*heroScale,hero.height*heroScale);
    const spriteX=Math.round(bx-C*scale*(sheet.pivot?.[0]||.53)),spriteY=Math.round(by-CH*scale*(sheet.pivot?.[1]||.77));
    const anchor=manifest.clips['infernal-shot'].muzzleAnchors?.[state.frame]||[.72,.2];
    const tx=state.name==='infernal-shot'?spriteX+anchor[0]*C*scale:bx-18;
    const ty=state.name==='infernal-shot'?spriteY+anchor[1]*CH*scale:by-146;
    if(state.name==='star-barrage'){
      const spots=[[hx,hy-18],[350,205],[395,405],[160,190],[510,410]],n=phase.value==='crimson'?5:3;
      for(let i=0;i<n;i++){const [x,y]=spots[i];ring(x,y,21,'#ee6a54',3);const hit=state.t-(800+i*(phase.value==='crimson'?100:180));if(hit>=0&&hit<350){line(x+34,y-140*(1-hit/350),x,y,'#ffce75',6);ring(x,y,12+hit*.065,'#f8ba68',3);}}
    }
    if(state.name==='infernal-shot'){
      if(laser.aiming){ring(hx,hy-18,25,'#f36c59',3);line(hx-35,hy-18,hx+35,hy-18,'#f36c59');line(hx,hy-53,hx,hy+17,'#f36c59');line(tx,ty,hx,hy-18,laser.locked?'#effaff':'#a4c0ce',1);}
    }
    if(state.name==='piercing-sting'&&state.t<1000)line(bx-40,by-18,bx-218,by+8,'#ec8b62',5);
    if(state.name==='needle-volley'&&state.t>=280&&state.t<1150){
      const n=phase.value==='crimson'?10:6;
      for(let i=0;i<n;i++){const p=(state.t-280-i*(phase.value==='crimson'?65:110))/390;if(p>=0&&p<=1){const x=tx+(hx-tx)*p,y=ty+(hy-ty)*p;line(x+9,y-4,x-9,y+4,'#ffd190',3);}}
    }
    const source=[...(sheet.rects?.[state.row]?.[state.sourceFrame]||[state.sourceFrame*C,state.row*CH,C,CH])];
    const inset=state.clip.frameInsetTop?.[state.frame]||0;
    const trim=Math.round(source[3]*inset);
    source[1]+=trim;source[3]-=trim;
    ctx.drawImage(images[state.sheet],...source,spriteX,spriteY+Math.round(CH*scale*inset),Math.round(C*scale),Math.round(CH*scale*(1-inset)));
    if(laser)drawLaser(tx,ty,hx,hy-18,laser);
    if(state.name==='slam'&&state.t>=800&&state.t<1200){const p=(state.t-800)/400;ring(bx-54,by+9,24+p*102,'#edbe77',3);spark(bx-54,by+9,p,'#c2ad7e');}
    if(state.name==='tail-regrow'&&state.frame===5){const p=(state.t-1200)/600;ring(bx,by-30,24+Math.max(0,p)*100,'#eebf71',3);spark(bx,by-30,Math.max(0,p),'#ebd999');}
    if(state.name==='tail-break'&&state.frame>=1&&state.frame<=3)spark(bx+30,by-90,(state.t%300)/300,'#f6eccb',12);
    if(state.name==='enrage'&&state.frame>=2)spark(bx,by-65,(state.t%500)/500,'#f7eac1',16);
    if(state.action==='armor'||state.name==='piercing-sting'&&state.frame===4){
      for(const [dx,dy]of [[24,-92],[-52,-43],[69,-39]])ring(bx+dx,by+dy,9,'#e9dc8d',2);
    }
    ctx.font='18px Arial';ctx.fillStyle='#e8e1c7';ctx.fillText('Герой',hx-28,hy+30);
    ctx.fillText(state.name==='defeat'&&state.frame>=4?'Маленькая Шаула':'Шаула',bx-37,by+78);
    ctx.restore();
    ctx.font='15px Arial';ctx.fillStyle='#c9d4bc';ctx.fillText('Ширина взрослого силуэта: 6 высот героя',30,490);
    scrub.value=String(state.frame);counter.textContent=(state.frame+1)+' / 6';clock.textContent=(state.t/1000).toFixed(2).replace('.',',')+' с';
    const description=logic.status(state);if(description!==lastStatus){status.textContent=description;lastStatus=description;}
  }
  function tick(now){if(ready&&playing){if(last)elapsed+=Math.min(80,now-last);const state=logic.resolve(manifest,action.value,phase.value,elapsed);if(elapsed>=state.duration){if(state.loop)elapsed%=state.duration;else{elapsed=state.duration-1;playing=false;play.textContent='Воспроизвести';}}draw();}last=now;requestAnimationFrame(tick);}
  action.addEventListener('change',()=>{playing=true;play.textContent='Пауза';reset();});phase.addEventListener('change',reset);
  play.addEventListener('click',()=>{const state=logic.resolve(manifest,action.value,phase.value,elapsed);if(!playing&&elapsed>=state.duration-2)elapsed=0;playing=!playing;play.textContent=playing?'Пауза':'Воспроизвести';last=0;draw();});
  restart.addEventListener('click',()=>{playing=true;play.textContent='Пауза';reset();});
  scrub.addEventListener('input',()=>{const state=logic.resolve(manifest,action.value,phase.value,0);elapsed=state.clip.durationsMs.slice(0,Number(scrub.value)).reduce((a,b)=>a+b,0)+1;playing=false;play.textContent='Воспроизвести';draw();});
  window.ShaulaPreview={select(name,form){action.value=name;phase.value=form;reset();},seek(ms){elapsed=ms;draw();},get ready(){return ready;}};
  Promise.all(Object.entries(manifest.sheets).map(async([key,value])=>{images[key]=await image(value.file);})).then(async()=>{images.hero=await image(manifest.hero.file);ready=true;play.textContent=playing?'Пауза':'Воспроизвести';draw();requestAnimationFrame(tick);}).catch(error=>{status.textContent=error.message;});
})();
