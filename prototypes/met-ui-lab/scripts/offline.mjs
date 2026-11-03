import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve('dist');
let html=fs.readFileSync(path.join(root,'index.html'),'utf8');
html=html.replace(/<script\b[^>]*src="([^"]+)"[^>]*><\/script>/g,(_,url)=>'<script type="module">'+fs.readFileSync(path.join(root,url),'utf8').replace(/<\/script/gi,'<\\/script')+'</script>');
html=html.replace(/<link\b[^>]*rel="stylesheet"[^>]*>/g,tag=>{const url=tag.match(/href="([^"]+)"/)?.[1];return url?'<style>'+fs.readFileSync(path.join(root,url),'utf8')+'</style>':tag});
html=html.replace(/<link\b[^>]*rel="modulepreload"[^>]*>/g,'');
fs.writeFileSync(path.join(root,'index.html'),html);
console.log('Offline index.html ready — bundled scripts, styles and map image.');
