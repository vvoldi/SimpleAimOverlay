import { page } from './page.mjs';
const repo='vvoldi/SimpleAimOverlay';
const keys=['App version','Windows','Architecture','Runtime','UI scale','Language','Theme','Displays','Crosshair','Geometry','Appearance','Behavior'];
const neutral=s=>s.replace(/@/g,'@\u200b').replace(/</g,'&lt;').replace(/>/g,'&gt;');
export function issueFrom(input){
  if(!input||!['bug','suggestion'].includes(input.kind)||typeof input.description!=='string')throw Error('invalid');
  const description=input.description.trim();
  if(description.length<10||description.length>2000)throw Error('invalid');
  const diagnostics=input.diagnostics??{};
  if(typeof diagnostics!=='object'||Array.isArray(diagnostics))throw Error('invalid');
  const type=input.kind==='bug'?'Bug report':'Suggestion';
  const title=`[${type}] ${neutral(description.replace(/\s+/g,' ')).slice(0,90)}`;
  const rows=keys.filter(k=>Object.hasOwn(diagnostics,k)).map(k=>{
    const v=diagnostics[k];if(typeof v!=='string'||v.length>600)throw Error('invalid');
    return `| ${k} | ${neutral(v).replace(/[|\r\n`]/g,' ')} |`;
  });
  const quoted=neutral(description).split('\n').map(line=>'> '+line).join('\n');
  return {title,body:`## ${type}\n\n${quoted}\n\n## Technical details\n\n${rows.length?'| Field | Value |\n| --- | --- |\n'+rows.join('\n'):'The sender chose not to include technical details.'}\n\n---\nSubmitted through the Simple Aim Overlay feedback form by an anonymous user. Posted by the project owner’s service; this is not a personal statement by the account owner. Diagnostics are supplied by the client and may be incomplete.`,labels:[input.kind==='bug'?'bug':'enhancement']};
}
const headers={'Cache-Control':'no-store','Referrer-Policy':'no-referrer','X-Content-Type-Options':'nosniff'};
const json=(body,status=200)=>Response.json(body,{status,headers});
export default {
  async fetch(request,env){
    const url=new URL(request.url);
    if(request.method==='GET'&&url.pathname==='/')return new Response(page(env.TURNSTILE_SITE_KEY||''),{headers:{...headers,'Content-Type':'text/html; charset=utf-8','Content-Security-Policy':"default-src 'none'; script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; style-src 'unsafe-inline'; frame-src https://challenges.cloudflare.com; connect-src 'self' https://challenges.cloudflare.com; base-uri 'none'; form-action 'self'; frame-ancestors 'none'"}});
    if(request.method!=='POST'||url.pathname!=='/api/feedback')return json({error:'not_found'},404);
    if(!env.GITHUB_TOKEN||!env.TURNSTILE_SECRET||!env.TURNSTILE_HOSTNAME)return json({error:'not_configured'},503);
    if(request.headers.get('Origin')!==url.origin||!request.headers.get('Content-Type')?.startsWith('application/json'))return json({error:'forbidden'},403);
    try {
      // Bound the stream as well as Content-Length (which clients may omit).
      const reader=request.body?.getReader();if(!reader)return json({error:'invalid'},400);
      const chunks=[];let size=0;
      while(true){const {done,value}=await reader.read();if(done)break;size+=value.length;if(size>16000){await reader.cancel();return json({error:'too_large'},413);}chunks.push(value);}
      const bytes=new Uint8Array(size);let offset=0;for(const chunk of chunks){bytes.set(chunk,offset);offset+=chunk.length;}
      const input=JSON.parse(new TextDecoder().decode(bytes));
      const issue=issueFrom(input.report);
      if(typeof input.token!=='string'||input.token.length<1||input.token.length>2048)return json({error:'verification'},403);
      const ip=request.headers.get('CF-Connecting-IP')||'unknown';
      if(!(await env.FEEDBACK_LIMIT.limit({key:ip})).success||!(await env.TOTAL_LIMIT.limit({key:'all'})).success)return json({error:'rate_limit'},429);
      const verify=await fetch('https://challenges.cloudflare.com/turnstile/v0/siteverify',{method:'POST',body:new URLSearchParams({secret:env.TURNSTILE_SECRET,response:input.token,remoteip:ip}),signal:AbortSignal.timeout(10000)});
      if(!verify.ok)return json({error:'verification'},403);
      const verdict=await verify.json();
      if(!verdict.success||verdict.hostname!==env.TURNSTILE_HOSTNAME||verdict.action!=='feedback')return json({error:'verification'},403);
      // No automatic retry: a timeout may mean GitHub already created the issue.
      const response=await fetch(`https://api.github.com/repos/${repo}/issues`,{method:'POST',headers:{Authorization:`Bearer ${env.GITHUB_TOKEN}`,Accept:'application/vnd.github+json','User-Agent':'SimpleAimOverlay-Feedback','Content-Type':'application/json','X-GitHub-Api-Version':'2022-11-28'},body:JSON.stringify(issue),signal:AbortSignal.timeout(15000)});
      if(response.status!==201)return json({error:'github_unavailable'},502);
      const created=await response.json();
      if(!Number.isSafeInteger(created.number)||created.number<1)return json({error:'github_unavailable'},502);
      return json({url:`https://github.com/${repo}/issues/${created.number}`},201);
    } catch(error){return json({error:error.message==='invalid'||error instanceof SyntaxError?'invalid':'temporary_failure'},error.message==='invalid'||error instanceof SyntaxError?400:502);}
  }
};
