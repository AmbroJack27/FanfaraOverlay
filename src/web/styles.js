// Fanfara — the notification styles and their categories, shared by settings.html and home.html.
// [key, display name]. Keys are what Config.Style stores and what notify.html renders.
window.FAN_STYLES=[['fantasy','Fantasy'],['xbox','Xbox'],['playstation','PlayStation'],['switch','Switch'],['mh','Monster Hunter'],['sf','Street Fighter'],['mm','Mega Man'],['dmc','Devil May Cry'],['dd',"Dragon's Dogma"],['dr','Dead Rising'],['dc','Dino Crisis'],['re','Resident Evil'],['ff','Final Fantasy'],['ct','Chrono Trigger'],['cc','Chrono Cross'],['xg','Xenogears'],['vs','Vagrant Story'],['som','Secret of Mana'],['kh','Kingdom Hearts'],['tr','Tomb Raider'],['dx','Deus Ex'],['ha','Halo'],['fz','Forza'],['gw','Gears of War'],['fb','Fable'],['ae','Age of Empires'],['mc','Minecraft'],['ac',"Assassin's Creed"],['r6','Rainbow Six'],['gr','Ghost Recon'],['td','The Division'],['sc','Splinter Cell'],['fc','Far Cry'],['ry','Rayman'],['an','Anno'],['wi','The Witcher'],['cp','Cyberpunk 2077'],['on','Onda'],['pu','Pulsar'],['mo','Mosaico'],['nu','Numero'],['ca','Carica'],['co','Coriandoli'],['ne','Nebula'],['tt','Tratto'],['pg','Piega'],['ob','Orbita']];
// Categories shown as big boxes. `rep` is the style previewed on the box; Base spans two columns.
window.FAN_CATS=[
  {id:'base',      name:'Base',        c:'#F5C242', rep:'fantasy', span:2, list:['fantasy','on','pu','mo','nu','ca','co','ne','tt','pg','ob']},
  {id:'console',   name:'',            c:'#2fd0c6', rep:'xbox',            list:['xbox','playstation','switch']},
  {id:'capcom',    name:'Capcom',      c:'#ff6b5a', rep:'mh',              list:['mh','sf','mm','dmc','dd','dr','dc','re']},
  {id:'square',    name:'Square-Enix', c:'#B078FF', rep:'ff',              list:['ff','ct','cc','xg','vs','som','kh','tr','dx']},
  {id:'microsoft', name:'Microsoft',   c:'#4ac46a', rep:'ha',              list:['ha','fz','gw','fb','ae','mc']},
  {id:'ubisoft',   name:'Ubisoft',     c:'#4C8DFF', rep:'ac',              list:['ac','r6','gr','td','sc','fc','ry','an']},
  {id:'cdprojekt', name:'CD Projekt',  c:'#ff4d8d', rep:'cp',              list:['wi','cp']},
];
