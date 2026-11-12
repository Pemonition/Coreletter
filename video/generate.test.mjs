import test from 'node:test';
import assert from 'node:assert/strict';
import {mkdtemp,readFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {join} from 'node:path';
import {compose,generate} from './generate.mjs';
const demo = JSON.parse(await readFile(new URL('./demo.json',import.meta.url),'utf8'));
test('escapes untrusted copy and uses a bounded deterministic timeline',()=>{
 const d=structuredClone(demo); d.scenes[0].heading='<script>alert(1)</script>';
 const html=compose(d); assert.ok(html.includes('&lt;script&gt;')); assert.ok(!html.includes('<script>alert(1)</script>')); assert.ok(html.includes('data-duration="26"')); assert.equal(html,compose(d));
});
test('rejects incompatible scripts, excessive scenes, durations and unsafe sources',()=>{
 for (const mutate of [d=>d.simulated=false,d=>d.scenes=[],d=>d.scenes[0].seconds=Infinity,d=>d.scenes[0].seconds=2,d=>d.scenes[0].sources=[{name:'X',url:'javascript:alert(1)'}],d=>d.scenes[0].sources=[{name:'X',url:'https://user:pass@example.com'}]]) {const d=structuredClone(demo);mutate(d);assert.throws(()=>compose(d));}
});
test('refuses to overwrite a composition already edited by a teammate',async()=>{
 const folder=await mkdtemp(join(tmpdir(),'coreletter-video-'));
 try {await generate(new URL('./demo.json',import.meta.url),folder); const before=await readFile(join(folder,'index.html'),'utf8'); await assert.rejects(generate(new URL('./demo.json',import.meta.url),folder),{code:'EEXIST'});assert.equal(await readFile(join(folder,'index.html'),'utf8'),before);}finally{await rm(folder,{recursive:true,force:true});}
});
