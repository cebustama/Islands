const fs=require('fs');const html=fs.readFileSync(process.argv[2],'utf8');
const js=html.slice(html.indexOf('<script>')+8,html.lastIndexOf('</script>'));
const arcs=[];let rafs=[];const els={};
function ctx(){return new Proxy({},{get:(t,k)=>{if(k==='arc')return (x,y)=>arcs.push([x,y]);if(k==='createImageData')return (w,h)=>({data:new Uint8ClampedArray(w*h*4)});if(k==='canvas')return{};return typeof k==='string'&&/Style|Width|font|imageSmoothing/.test(k)?t[k]:(()=>{});},set:(t,k,v)=>{t[k]=v;return true}});}
function el(id){return els[id]||(els[id]={id,width:960,height:540,value:'',textContent:'',getContext:ctx,addEventListener(){},setPointerCapture(){},getBoundingClientRect:()=>({left:0,top:0,width:960,height:540}),onclick:null});}
global.document={getElementById:el,createElement:()=>el('off'+Math.random()),documentElement:{}};
global.getComputedStyle=()=>({getPropertyValue:()=>'#000'});global.matchMedia=()=>({});global.MutationObserver=class{observe(){}};
let now=0;global.performance={now:()=>now};global.requestAnimationFrame=f=>rafs.push(f);
eval(js);
for(let f=0;f<600;f++){const cur=rafs;rafs=[];now+=16;arcs.length=0;cur.forEach(fn=>fn(f%60===59?now+2000:0));
 if(f%120===119){const nan=arcs.filter(a=>!isFinite(a[0])||!isFinite(a[1])).length;const ys=arcs.map(a=>a[1]).filter(isFinite);ys.sort((a,b)=>a-b);
  console.log('frame',f,'sph arcs',arcs.length,'NaN',nan,'y p10/p50/p90',ys[ys.length*.1|0]|0,ys[ys.length/2|0]|0,ys[ys.length*.9|0]|0,'| grid:',el('grid-stat').textContent);}}
