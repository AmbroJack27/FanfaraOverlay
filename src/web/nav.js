/* Fanfara 0.7.0 — the navigation bar at the top of every screen (Home, Collection, History, Leaderboard,
   Settings, Guide, Feedback). Each page calls FanNav.mount('<key>') once; the bar takes the place of the
   page's old «Home» button and title, inside its sticky top bar. Theme and language travel in the link. */
(function(){
  var T={
    it:{home:'Home',coll:'Collezione',hist:'Cronologia',rank:'Classifica',set:'Impostazioni',guide:'Guida',fb:'Feedback'},
    en:{home:'Home',coll:'Collection',hist:'History',rank:'Leaderboard',set:'Settings',guide:'Guide',fb:'Feedback'},
    fr:{home:'Accueil',coll:'Collection',hist:'Historique',rank:'Classement',set:'Réglages',guide:'Guide',fb:'Avis'},
    de:{home:'Start',coll:'Sammlung',hist:'Verlauf',rank:'Rangliste',set:'Einstellungen',guide:'Anleitung',fb:'Feedback'},
    es:{home:'Inicio',coll:'Colección',hist:'Historial',rank:'Clasificación',set:'Ajustes',guide:'Guía',fb:'Opinión'}
  };
  var P='<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">';
  var IC={
    home:'<path d="M3 11l9-8 9 8M5 10v10h5v-6h4v6h5V10"/>',
    coll:'<rect x="3" y="4" width="13" height="16" rx="2"/><path d="M8 2.5l10 2.2a2 2 0 0 1 1.5 2.4l-2.3 10.6"/>',
    hist:'<path d="M3 12a9 9 0 1 0 3-6.7M3 4v4h4"/><path d="M12 8v4l3 2"/>',
    rank:'<circle cx="12" cy="8" r="5"/><path d="M8.5 12.5 7 22l5-3 5 3-1.5-9.5"/>',
    set:'<path d="M4 7h10M18 7h2M4 12h2M10 12h10M4 17h7M15 17h5"/><circle cx="16" cy="7" r="2"/><circle cx="8" cy="12" r="2"/><circle cx="13" cy="17" r="2"/>',
    guide:'<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20V3H6.5A2.5 2.5 0 0 0 4 5.5z"/>',
    fb:'<path d="M21 12a8 8 0 0 1-11.6 7.1L4 20l1-4.6A8 8 0 1 1 21 12z"/>'
  };
  var TROPHY=P+'<path d="M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0V4zM17 5h3v2a3 3 0 0 1-3 3M7 5H4v2a3 3 0 0 0 3 3"/></svg>';
  var PAGES={home:'home.html',coll:'collection.html',hist:'history.html',rank:'classifica.html',set:'settings.html',guide:'guide.html',fb:'feedback.html'};
  var ORDER=['home','coll','hist','rank','set','sep','guide','fb'];

  function lang(){ var m=location.search.match(/[?&]lang=([a-z]{2})/), l=(m&&m[1])||document.documentElement.lang||'it'; return T[l]?l:'en'; }
  function theme(){
    var app=document.getElementById('app'); if(app && app.dataset && app.dataset.mode) return app.dataset.mode==='light'?'light':'dark';
    return document.documentElement.getAttribute('data-theme')==='dark'?'dark':'light';
  }
  function go(key){ var u=PAGES[key]+'?theme='+theme()+'&lang='+lang(); if(key==='home') u+='&home=1'; location.href=u; }

  var CSS=
    '.fnav{display:flex; align-items:center; gap:4px; min-width:0}'+
    '.fnav .fb{display:flex; align-items:center; gap:9px; margin-right:14px; font:800 18px var(--display,"Oxanium","Segoe UI",sans-serif); white-space:nowrap}'+
    '.fnav .fb svg{width:24px; height:24px; color:#F5C242; filter:drop-shadow(0 0 8px rgba(245,194,66,.35))}'+
    ':root:not([data-theme="dark"]) .fnav .fb svg{color:#c8912a}'+
    '.fnav a{display:flex; align-items:center; gap:8px; padding:8px 12px; border-radius:11px; border:1px solid transparent; cursor:pointer; text-decoration:none; color:inherit; opacity:.68; font:600 13.5px var(--body,"Archivo","Segoe UI",sans-serif); white-space:nowrap; transition:opacity .15s, background .15s}'+
    '.fnav a svg{width:17px; height:17px; flex:0 0 auto}'+
    '.fnav a:hover{opacity:1; background:rgba(128,128,128,.1)}'+
    '.fnav a.on{opacity:1; background:rgba(245,194,66,.13); border-color:rgba(245,194,66,.42)}'+
    '.fnav a.on svg{color:#F5C242} :root:not([data-theme="dark"]) .fnav a.on svg{color:#c8912a}'+
    '.fnav i.sp{width:1px; height:22px; background:currentColor; opacity:.15; margin:0 6px}'+
    '.fnav-on > #back, .fnav-on > .back, .fnav-on > .brand, .fnav-on > #homeBtn, .fnav-on > .badge, .fnav-on > .htitle{display:none!important}'+
    '@media (max-width:1240px){ .fnav a span{display:none} .fnav a{padding:9px} .fnav a.on span{display:inline} }'+
    '@media (max-width:900px){ .fnav a.on span{display:none} .fnav .fb b{display:none} }';

  window.FanNav={
    mount:function(active, host){
      host=host||document.querySelector('.top .wrap')||document.querySelector('.top')||document.querySelector('.head');
      if(!host || host.querySelector('.fnav')) return;
      if(!document.getElementById('fnav-css')){ var st=document.createElement('style'); st.id='fnav-css'; st.textContent=CSS; document.head.appendChild(st); }
      var t=T[lang()], nav=document.createElement('nav');
      nav.className='fnav'; nav.setAttribute('aria-label','Fanfara');
      nav.innerHTML='<div class="fb">'+TROPHY+'<b>Fanfara</b></div>'+ORDER.map(function(k){
        if(k==='sep') return '<i class="sp"></i>';
        return '<a role="link" tabindex="0" data-k="'+k+'"'+(k===active?' class="on" aria-current="page"':'')+' title="'+t[k]+'">'+P+IC[k]+'</svg><span>'+t[k]+'</span></a>';
      }).join('');
      nav.addEventListener('click',function(e){ var a=e.target.closest('a[data-k]'); if(!a) return; e.preventDefault(); if(a.dataset.k!==active || active==='coll') go(a.dataset.k); });
      nav.addEventListener('keydown',function(e){ var a=e.target.closest('a[data-k]'); if(a && (e.key==='Enter'||e.key===' ')){ e.preventDefault(); go(a.dataset.k); } });
      host.classList.add('fnav-on');
      host.insertBefore(nav, host.firstChild);
      return nav;
    },
    // pages that change language without reloading can call this to relabel the bar
    relabel:function(l){ var t=T[l]||T.en; document.querySelectorAll('.fnav a[data-k]').forEach(function(a){ a.title=t[a.dataset.k]; a.querySelector('span').textContent=t[a.dataset.k]; }); }
  };
})();
