const test=require('node:test'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const model=require('./preview-logic.js');
const data=JSON.parse(fs.readFileSync(path.join(__dirname,'animations.json'),'utf8'));
test('both forms use the new white-laser sprite row',()=>{
  for(const [phase,row]of [['normal',0],['crimson',1]]){
    const s=model.resolve(data,'infernal-shot',phase,1520);
    assert.equal(s.sheet,'laser');assert.equal(s.row,row);assert.equal(s.frame,3);
    assert.equal(data.clips[s.name].beamColor,'#ffffff');
  }
});
test('laser starts after a full 1500ms charge and aim locks 400ms before it',()=>{
  assert.equal(model.laserAt(1099).locked,false);assert.equal(model.laserAt(1100).locked,true);
  assert.equal(model.laserAt(1499).firing,false);assert.equal(model.laserAt(1500).firing,true);
  assert.equal(model.laserAt(1500).beamAlpha,1);assert.equal(model.laserAt(1500).locked,false);
});
test('beam is a brief flash; impact fades independently',()=>{
  assert.equal(model.laserAt(1679).firing,true);assert.equal(model.laserAt(1680).firing,false);
  assert.equal(model.laserAt(1680).beamAlpha,0);assert.ok(model.laserAt(1800).impact>0);
  assert.equal(model.laserAt(1950).impact,-1);assert.equal(model.laserAt(1950).kick,0);
});
test('sting retains its one-second warning',()=>{
  assert.equal(model.resolve(data,'piercing-sting','normal',999).frame,1);
  assert.equal(model.resolve(data,'piercing-sting','normal',1000).frame,2);
});
test('every selected action and phase stays within an existing sprite rectangle',()=>{
  for(const phase of ['normal','crimson'])for(const name of [...Object.keys(data.clips),'chase','armor']){
    const first=model.resolve(data,name,phase,0);
    for(let t=0;t<=first.duration;t+=31){const s=model.resolve(data,name,phase,t),sheet=data.sheets[s.sheet];
      assert.ok(s.frame>=0&&s.frame<6);assert.ok(s.sourceFrame>=0&&s.sourceFrame<sheet.columns);
      const [x,y,w,h]=sheet.rects[s.row][s.sourceFrame];assert.ok(x+w<=sheet.width&&y+h<=sheet.height);
      assert.ok(fs.existsSync(path.join(__dirname,sheet.file)));
    }
  }
});
test('the three-dash combo ends with its separate recovery window',()=>{
  const total=data.clips.grab.totalMs*3;
  assert.equal(model.resolve(data,'chase','normal',total-1).cycle,2);
  assert.match(model.status(model.resolve(data,'chase','normal',total)),/окно уязвимости/);
});
test('defeat always uses crimson sprites and ends in the small harmless form',()=>{
  const s=model.resolve(data,'defeat','normal',data.clips.defeat.totalMs-1);
  assert.equal(s.sheet,'special-crimson');assert.equal(s.frame,5);assert.equal(s.scale,.45);
  assert.match(model.status(s),/больше не атакует/);
});
test('black form stays black after tail regeneration finishes',()=>{
  const state=model.resolve(data,'tail-regrow','normal',data.clips['tail-regrow'].totalMs-1);
  assert.equal(state.sheet,'normal');assert.equal(state.row,0);assert.equal(state.sourceFrame,0);
});
test('rounded atlas rectangles cover every cell without reading outside the image',()=>{
  for(const sheet of Object.values(data.sheets))for(let r=0;r<sheet.rows;r++)for(let c=0;c<sheet.columns;c++){
    const [x,y,w,h]=sheet.rects[r][c];
    assert.ok(Number.isInteger(x)&&Number.isInteger(y)&&Number.isInteger(w)&&Number.isInteger(h));
    assert.ok(x>=0&&y>=0&&w>0&&h>0&&x+w<=sheet.width&&y+h<=sheet.height);
    if(c>0){const prev=sheet.rects[r][c-1];assert.equal(prev[0]+prev[2],x);}
    if(c===sheet.columns-1)assert.equal(x+w,sheet.width);
  }
});
