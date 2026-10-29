import {test} from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import worker,{issueFrom} from '../src/worker.mjs';
import {page} from '../src/page.mjs';
const origin='https://feedback.example';
const report={kind:'bug',description:'Приціл зникає після перемикання.',diagnostics:{'App version':'0.2.4','Windows':'Windows 11','secret':'must-not-publish'},language:'uk'};
const env={GITHUB_TOKEN:'unit-test-only',TURNSTILE_SECRET:'unit-test-only',TURNSTILE_HOSTNAME:'feedback.example',FEEDBACK_LIMIT:{limit:async()=>({success:true})},TOTAL_LIMIT:{limit:async()=>({success:true})}};
const request=(payload={report,token:'test-token'},headers={})=>new Request(origin+'/api/feedback',{method:'POST',headers:{Origin:origin,'Content-Type':'application/json','CF-Connecting-IP':'192.0.2.1',...headers},body:JSON.stringify(payload)});
test('page browser script parses; sitekey cannot inject HTML',()=>{new vm.Script(page('public').match(/<script>([\s\S]*?)<\/script>/)[1]);assert(!page('</script><img>').includes('</script><img>'));});
test('issue formatting, allowlist, mentions, validation',()=>{let issue=issueFrom({...report,description:'Broken @everyone <script> please fix'});assert(issue.body.includes('Windows 11'));assert(!issue.body.includes('must-not-publish'));assert(!issue.body.includes('@everyone'));assert(!issue.body.includes('<script>'));assert.deepEqual(issue.labels,['bug']);assert.throws(()=>issueFrom({...report,description:'short'}));assert.throws(()=>issueFrom({...report,description:'x'.repeat(2001)}));assert.deepEqual(issueFrom({...report,kind:'suggestion'}).labels,['enhancement']);});
test('unconfigured service fails closed',async()=>{assert.equal((await worker.fetch(request(),{})).status,503);});
test('cross-origin and huge requests rejected',async()=>{assert.equal((await worker.fetch(request(undefined,{Origin:'https://evil.example'}),env)).status,403);assert.equal((await worker.fetch(request({report,token:'x'.repeat(18000)}),env)).status,413);});
test('rate limit prevents any outbound requests',async()=>{assert.equal((await worker.fetch(request(),{...env,FEEDBACK_LIMIT:{limit:async()=>({success:false})}})).status,429);});
test('verified report creates exactly one issue at the fixed repository',async()=>{
  const original=globalThis.fetch;const calls=[];
  globalThis.fetch=async(url,options)=>{calls.push([url,options]);if(url.includes('siteverify'))return Response.json({success:true,hostname:'feedback.example',action:'feedback'});return Response.json({number:123},{status:201});};
  try{const response=await worker.fetch(request(),env);assert.equal(response.status,201);assert.equal((await response.json()).url,'https://github.com/vvoldi/SimpleAimOverlay/issues/123');assert.equal(calls.length,2);assert.equal(calls[1][0],'https://api.github.com/repos/vvoldi/SimpleAimOverlay/issues');assert(!calls[1][1].body.includes('must-not-publish'));}finally{globalThis.fetch=original;}
});
test('wrong Turnstile hostname/action never reaches GitHub',async()=>{
  const original=globalThis.fetch;let count=0;globalThis.fetch=async()=>{count++;return Response.json({success:true,hostname:'wrong.example',action:'feedback'});};
  try{assert.equal((await worker.fetch(request(),env)).status,403);assert.equal(count,1);}finally{globalThis.fetch=original;}
});
test('upstream timeout does not retry or expose secret',async()=>{
  const original=globalThis.fetch;let count=0;globalThis.fetch=async()=>{count++;if(count===1)return Response.json({success:true,hostname:'feedback.example',action:'feedback'});throw Error('unit-test-only secret');};
  try{const response=await worker.fetch(request(),env);assert.equal(response.status,502);assert(!((await response.text()).includes('unit-test-only')));assert.equal(count,2);}finally{globalThis.fetch=original;}
});
