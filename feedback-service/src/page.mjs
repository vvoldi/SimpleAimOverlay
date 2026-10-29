export function page(sitekey){
  const safeKey=JSON.stringify(sitekey).replace(/</g,'\\u003c');
  return String.raw`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Simple Aim Overlay · Feedback</title>
<style>body{color:#ebf0f6;background:#0f1217;font:16px system-ui;margin:0;padding:32px 16px}main{max-width:720px;margin:auto;background:#161b23;padding:28px;border-radius:20px}h1{font-size:26px}p{line-height:1.5;color:#a9b7c9}pre{background:#131820;padding:20px;border-radius:12px;white-space:pre-wrap;overflow-wrap:anywhere;max-height:45vh;overflow:auto;font:14px/1.6 system-ui}button,a{color:#0f1217;background:#45dc99;padding:14px 22px;border:0;border-radius:10px;font:600 16px system-ui;cursor:pointer;display:inline-block}button:disabled{opacity:.45;cursor:default}#status{min-height:25px}label{display:block;line-height:1.5;margin:20px 0}#result{display:none}small{color:#a9b7c9}</style>
<main><small>SIMPLE AIM OVERLAY</small><h1 id="heading">Review your feedback</h1><p id="intro">Your report will be public on GitHub. It is posted by the project service; you do not need a GitHub account.</p><pre id="preview"></pre><label><input type="checkbox" id="consent"> <span id="consentText">I agree to publish this description and the technical details shown above.</span></label><div id="challenge"></div><button id="send" disabled>Send feedback</button><p id="status" role="status"></p><a id="result" rel="noopener noreferrer">View issue on GitHub</a></main>
<script>
const $=id=>document.getElementById(id);let report,token='',widget,busy=false,done=false;
const uk=()=>report?.language==='uk';const t=(en,ua)=>uk()?ua:en;
function ready(){ $('send').disabled=busy||done||!report||!token||!$('consent').checked; }
try {let raw=location.hash.slice(1);if(raw.length>22000)throw Error();raw=raw.replace(/-/g,'+').replace(/_/g,'/');report=JSON.parse(new TextDecoder().decode(Uint8Array.from(atob(raw),c=>c.charCodeAt(0))));
if(!['bug','suggestion'].includes(report.kind)||typeof report.description!=='string'||report.description.trim().length<10||report.description.length>2000)throw Error();
history.replaceState(null,'',location.pathname);
$('preview').textContent=report.description+'\n\n'+Object.entries(report.diagnostics||{}).map(([k,v])=>k+': '+v).join('\n');
document.documentElement.lang=uk()?'uk':'en';
$('heading').textContent=t('Review your feedback','Перевір свій відгук');$('intro').textContent=t('Your report will be public on GitHub. It is posted by the project service; you do not need a GitHub account.','Звіт буде публічним на GitHub. Його надішле сервіс проєкту; тобі не потрібен акаунт GitHub.');
$('consentText').textContent=t('I agree to publish this description and the technical details shown above.','Погоджуюся опублікувати цей опис і технічні дані, показані вище.');$('send').textContent=t('Send feedback','Надіслати відгук');$('result').textContent=t('View issue on GitHub','Переглянути issue на GitHub');
}catch{report=null;$('status').textContent='Open this page from the app’s Feedback tab. / Відкрий сторінку з вкладки «Відгук» у програмі.';}
$('consent').onchange=ready;
window.onTurnstile=()=>{if(!report)return;const key=${safeKey};if(!key){$('status').textContent=t('Feedback service is being configured. Please try later.','Сервіс налаштовується. Спробуй пізніше.');return;}widget=turnstile.render('#challenge',{sitekey:key,action:'feedback',language:uk()?'uk':'en',callback:v=>{token=v;ready();},'expired-callback':()=>{token='';ready();},'error-callback':()=>{token='';ready();}});};
$('send').onclick=async()=>{if(!report||!token||!$('consent').checked||busy||done)return;busy=true;ready();$('status').textContent=t('Sending…','Надсилання…');try{
const response=await fetch('/api/feedback',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({report,token}),signal:AbortSignal.timeout(30000)});const result=await response.json();
if(!response.ok){const error=new Error();error.code=result.error;throw error;}
if(!/^https:\/\/github.com\/vvoldi\/SimpleAimOverlay\/issues\/\d+$/.test(result.url))throw Error();done=true;$('status').textContent=t('Thank you! Your issue has been created.','Дякуємо! Твій issue створено.');$('result').href=result.url;$('result').style.display='inline-block';
}catch(error){$('status').textContent=error.code==='rate_limit'?t('Too many reports. Wait a minute and try again.','Забагато звітів. Зачекай хвилину і повтори.'):error.code==='verification'?t('Please complete the verification again.','Пройди перевірку ще раз.'):t('Could not confirm delivery. Check GitHub issues before retrying to avoid duplicates. Your text remains in the app.','Не вдалося підтвердити надсилання. Перевір issues на GitHub перед повтором, щоб уникнути дублікатів. Текст залишився у програмі.');}
finally{busy=false;token='';if(widget!==undefined)turnstile.reset(widget);ready();}};
</script><script src="https://challenges.cloudflare.com/turnstile/v0/api.js?onload=onTurnstile&render=explicit" async defer></script></html>`;
}
