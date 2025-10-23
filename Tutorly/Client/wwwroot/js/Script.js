///* =========================
//   Tutorly Script.js (reconstructed)
//   Shared theme, nav/actions, profile pack + Messages page wiring
//   ========================= */

//(function () {
//    // ---------- THEME ----------
//    function initTheme() {
//        try {
//            const saved = localStorage.getItem('theme');
//            if (saved === 'dark') document.documentElement.classList.add('theme-dark');
//        } catch (_) { }
//    }

//    function toggleTheme() {
//        const el = document.documentElement;
//        const isDark = el.classList.toggle('theme-dark');
//        try { localStorage.setItem('theme', isDark ? 'dark' : 'light'); } catch (_) { }
//    }

//    // ---------- NAV UNDERLINE ----------
//    function wireNav() {
//        const nav = document.getElementById('mainNav');
//        if (!nav) return;
//        nav.addEventListener('click', (e) => {
//            const a = e.target.closest('a');
//            if (!a) return;
//            const href = (a.getAttribute('href') || '').trim();
//            [...nav.querySelectorAll('a')].forEach(x => x.classList.remove('db-nav__link--active'));
//            a.classList.add('db-nav__link--active');
//            if (href === '' || href === '#') e.preventDefault();
//        }, { passive: false });
//    }

//    // ---------- TOPBAR ACTIONS (search, notifications, profile pop) ----------
//    function wireActions() {
//        const sw = document.getElementById('searchWrap');
//        const sb = document.getElementById('btnSearch');
//        const si = document.getElementById('searchInput');
//        const bn = document.getElementById('btnNotif');
//        const pn = document.getElementById('popNotif');
//        const bp = document.getElementById('btnProfile');
//        const pp = document.getElementById('popProfile');

//        function closeAll() {
//            sw?.classList.remove('open');
//            pn?.classList.remove('show'); bn?.setAttribute('aria-expanded', 'false');
//            pp?.classList.remove('show'); bp?.setAttribute('aria-expanded', 'false');
//        }

//        sb?.addEventListener('click', (e) => {
//            e.preventDefault();
//            const o = sw.classList.toggle('open');
//            if (o) si?.focus();
//            pn?.classList.remove('show');
//            pp?.classList.remove('show');
//        });

//        bn?.addEventListener('click', (e) => {
//            e.preventDefault();
//            const s = pn.classList.toggle('show');
//            bn.setAttribute('aria-expanded', s ? 'true' : 'false');
//            pp?.classList.remove('show');
//            sw?.classList.remove('open');
//        });

//        bp?.addEventListener('click', (e) => {
//            e.preventDefault();
//            const s = pp.classList.toggle('show');
//            bp.setAttribute('aria-expanded', s ? 'true' : 'false');
//            pn?.classList.remove('show');
//            sw?.classList.remove('open');
//        });

//        document.addEventListener('click', (e) => {
//            const within = e.target.closest('#actionsBar');
//            const inLeft = e.target.closest('.db-topbar__left');
//            if (!(within || inLeft)) closeAll();
//        });
//    }

//    // ---------- PROFILE PACK (modals + local persistence) ----------
//    function initProfilePack() {
//        if (window.__tutorlyProfileInit) return; // avoid double-binding across pages
//        window.__tutorlyProfileInit = true;

//        const $ = (s) => document.querySelector(s);
//        const $$ = (s) => Array.from(document.querySelectorAll(s));

//        const avatarImg = $('#avatarImg');
//        const popProfile = $('#popProfile');

//        // Load persisted values
//        (function loadPersisted() {
//            try {
//                const p = localStorage.getItem('profile.photo');
//                if (p && avatarImg) avatarImg.src = p;

//                const fn = $('#firstName'), ln = $('#lastName'), dg = $('#degree');
//                if (fn) fn.value = localStorage.getItem('profile.first') || '';
//                if (ln) ln.value = localStorage.getItem('profile.last') || '';
//                if (dg) dg.value = localStorage.getItem('profile.degree') || '';

//                const prev = $('#photoPreview');
//                if (prev && p) prev.src = p;
//            } catch (_) { }
//        })();

//        // Open modals from profile menu
//        $$('#popProfile .clickable').forEach(li => {
//            li.addEventListener('click', () => {
//                const target = li.getAttribute('data-target');
//                if (target) document.querySelector(target)?.classList.add('show');
//                popProfile?.classList.remove('show');
//            });
//        });

//        // Close modals (backdrop/x/escape)
//        $$('.modal').forEach(m => m.addEventListener('click', (e) => { if (e.target === m) m.classList.remove('show'); }));
//        $$('[data-close]').forEach(btn => btn.addEventListener('click', () => {
//            const id = btn.getAttribute('data-close');
//            if (id) document.getElementById(id)?.classList.remove('show');
//        }));
//        document.addEventListener('keydown', (e) => {
//            if (e.key === 'Escape') $$('.modal.show').forEach(m => m.classList.remove('show'));
//        });

//        // Photo modal
//        const photoInput = $('#photoInput');
//        const photoPreview = $('#photoPreview');
//        $('#savePhotoBtn')?.addEventListener('click', () => {
//            try {
//                if (photoPreview?.src) {
//                    localStorage.setItem('profile.photo', photoPreview.src);
//                    if (avatarImg) avatarImg.src = photoPreview.src;
//                }
//            } catch (_) { }
//            document.getElementById('modalPhoto')?.classList.remove('show');
//        });
//        photoInput?.addEventListener('change', () => {
//            const f = photoInput.files?.[0]; if (!f) return;
//            const r = new FileReader();
//            r.onload = () => { if (photoPreview) photoPreview.src = r.result; };
//            r.readAsDataURL(f);
//        });

//        // Settings save
//        const fn = $('#firstName'), ln = $('#lastName'), dg = $('#degree');
//        $('#saveSettingsBtn')?.addEventListener('click', () => {
//            try {
//                localStorage.setItem('profile.first', fn?.value.trim() || '');
//                localStorage.setItem('profile.last', ln?.value.trim() || '');
//                localStorage.setItem('profile.degree', dg?.value || '');
//            } catch (_) { }
//            document.getElementById('modalSettings')?.classList.remove('show');
//        });
//    }

//    // ---------- MESSAGES PAGE ----------
//    const CONV_HTML = {
//        "mr-johnson": `
//      <div class="chip-day">Today</div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=11" alt=""></span>
//        <div class="bubble"><div>Hi there! How can I help you with your math homework today?</div><span class="time">10:30 AM</span></div>
//      </div>
//      <div class="msgrow me">
//        <div class="bubble"><div>Hi Mr. Johnson! I'm stuck on problem 12 from chapter 5.</div><span class="time">10:32 AM</span></div>
//      </div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=11" alt=""></span>
//        <div class="bubble"><div>Ah, that's a tricky one. Have you tried using the quadratic formula?</div><span class="time">10:33 AM</span></div>
//      </div>
//      <div class="msgrow me">
//        <div class="bubble"><div>Yes, but I keep getting a negative under the square root.</div><span class="time">10:35 AM</span></div>
//      </div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=11" alt=""></span>
//        <div class="bubble">
//          <div>That means there's no real solution. Check if you copied the equation correctly.</div>
//          <span class="time">10:36 AM</span>
//        </div>
//      </div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=11" alt=""></span>
//        <div class="bubble">
//          <div class="filecard">
//            <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
//              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
//              <path d="M14 2v6h6" />
//            </svg>
//            <div><div class="fname">Homework_Solutions.pdf</div><div class="fsize">2.4 MB</div></div>
//          </div>
//          <span class="time">10:37 AM</span>
//        </div>
//      </div>`,
//        "sarah-williams": `
//      <div class="chip-day">Yesterday</div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=5" alt=""></span>
//        <div class="bubble"><div>Did you finish the history assignment?</div><span class="time">3:10 PM</span></div>
//      </div>
//      <div class="msgrow me">
//        <div class="bubble"><div>Almost! I'm polishing the references. Can I send you my outline?</div><span class="time">3:12 PM</span></div>
//      </div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=5" alt=""></span>
//        <div class="bubble"><div>Sure, send it here and I’ll add comments.</div><span class="time">3:13 PM</span></div>
//      </div>`,
//        "study-group": `
//      <div class="chip-day">Mon</div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=7" alt=""></span>
//        <div class="bubble"><div>Alex: I'll bring the notes tomorrow.</div><span class="time">5:40 PM</span></div>
//      </div>
//      <div class="msgrow me">
//        <div class="bubble"><div>Great! Can someone book a study room?</div><span class="time">5:42 PM</span></div>
//      </div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=7" alt=""></span>
//        <div class="bubble"><div>Sam: Done — Library Room B at 2PM.</div><span class="time">5:45 PM</span></div>
//      </div>`,
//        "dr-smith": `
//      <div class="chip-day">Last week</div>
//      <div class="msgrow">
//        <span class="avatar"><img src="https://i.pravatar.cc/64?img=15" alt=""></span>
//        <div class="bubble"><div>Your essay was excellent!</div><span class="time">10:02 AM</span></div>
//      </div>
//      <div class="msgrow me">
//        <div class="bubble"><div>Thank you, Dr. Smith! Appreciate the feedback.</div><span class="time">10:05 AM</span></div>
//      </div>`
//    };

//    function wireMessages() {
//        const cb = document.getElementById('chatBody');
//        const ipt = document.getElementById('chatInput');
//        if (!cb || !ipt) return;

//        // Initial render
//        cb.innerHTML = CONV_HTML['mr-johnson'] || '<div class="chip-day">Today</div>';
//        cb.scrollTop = cb.scrollHeight;

//        // Send message
//        const sendBtn = document.getElementById('chatSend');
//        sendBtn?.addEventListener('click', () => {
//            const val = ipt.value.trim(); if (!val) return;
//            addTextBubble(val);
//        });
//        ipt.addEventListener('keydown', (e) => {
//            if (e.key === 'Enter') { e.preventDefault(); sendBtn?.click(); }
//        });

//        function addTextBubble(text) {
//            const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
//            const wrap = document.createElement('div');
//            wrap.className = 'msgrow me';
//            wrap.innerHTML = `<div class="bubble"><div>${escapeHtml(text)}</div><span class="time">${time}</span></div>`;
//            cb.appendChild(wrap);
//            ipt.value = '';
//            cb.scrollTop = cb.scrollHeight;
//        }

//        // New chat (+ Chat)
//        const pop = document.getElementById('newChatPop');
//        const btnNew = document.getElementById('btnNewChat');
//        const ncName = document.getElementById('newChatName');
//        const ncStart = document.getElementById('ncStart');
//        const ncCancel = document.getElementById('ncCancel');
//        const list = document.getElementById('threadList');

//        btnNew?.addEventListener('click', () => {
//            pop?.classList.toggle('show');
//            if (pop?.classList.contains('show')) setTimeout(() => ncName?.focus(), 0);
//        });
//        ncCancel?.addEventListener('click', () => pop?.classList.remove('show'));
//        document.addEventListener('click', (e) => {
//            const inSide = e.target.closest('.side');
//            const inPop = e.target.closest('#newChatPop');
//            const isBtn = e.target.closest('#btnNewChat');
//            if (inSide && !inPop && !isBtn) pop?.classList.remove('show');
//        });

//        ncStart?.addEventListener('click', () => {
//            const name = ncName.value.trim(); if (!name) return;
//            const seed = Math.floor(Math.random() * 70) + 1;
//            list?.querySelectorAll('.thread').forEach(li => li.classList.remove('thread--active'));
//            const li = document.createElement('li');
//            li.className = 'thread thread--active';
//            li.dataset.id = `c-${Date.now()}`;
//            li.dataset.name = name;
//            li.dataset.sub = 'Just now';
//            li.dataset.avatar = `https://i.pravatar.cc/64?img=${seed}`;
//            li.innerHTML = `<a href="#"><span class="avatar"><img src="https://i.pravatar.cc/64?img=${seed}" alt=""></span>
//                        <div><div class="tname">${escapeHtml(name)}</div><div class="tprev">New chat</div></div>
//                        <div class="tmeta">Just now</div></a>`;
//            list?.prepend(li);
//            openConversation(li);
//            pop?.classList.remove('show'); ncName.value = ''; ipt.focus();
//        });

//        list?.addEventListener('click', (e) => {
//            const a = e.target.closest('a'); if (!a) return;
//            e.preventDefault();
//            openConversation(a.closest('.thread'));
//        });

//        function openConversation(li) {
//            if (!li) return;
//            list.querySelectorAll('.thread').forEach(el => el.classList.remove('thread--active'));
//            li.classList.add('thread--active');
//            const id = li.dataset.id;
//            const name = li.dataset.name || li.querySelector('.tname')?.textContent?.trim() || 'Chat';
//            const sub = li.dataset.sub || '';
//            const avatar = li.dataset.avatar || li.querySelector('img')?.src || '';
//            const title = document.getElementById('chatTitle');
//            const subEl = document.getElementById('chatSub');
//            const img = document.getElementById('chatAvatar');

//            if (title) title.textContent = name;
//            if (subEl) subEl.textContent = sub;
//            if (img && avatar) img.src = avatar;

//            cb.innerHTML = CONV_HTML[id] || `<div class="chip-day">Today</div>`;
//            cb.scrollTop = cb.scrollHeight;
//        }

//        // Attachments
//        const btnAtt = document.getElementById('btnAttach');
//        const popAtt = document.getElementById('attachPop');
//        const fileImage = document.getElementById('fileImage');
//        const fileDoc = document.getElementById('fileDoc');
//        const fileVideo = document.getElementById('fileVideo');
//        const fileAudio = document.getElementById('fileAudio');
//        const fileAny = document.getElementById('fileAny');
//        const fileCamera = document.getElementById('fileCamera');

//        btnAtt?.addEventListener('click', (e) => {
//            e.preventDefault();
//            const open = popAtt.classList.toggle('show');
//            btnAtt.setAttribute('aria-expanded', open ? 'true' : 'false');
//        });
//        document.addEventListener('click', (e) => {
//            const within = e.target.closest('.attach');
//            if (!within) popAtt?.classList.remove('show');
//        });

//        popAtt?.addEventListener('click', (e) => {
//            const btn = e.target.closest('.att-item'); if (!btn) return;
//            const k = btn.getAttribute('data-kind');
//            if (k === 'image') fileImage.click();
//            else if (k === 'doc') fileDoc.click();
//            else if (k === 'video') fileVideo.click();
//            else if (k === 'audio') fileAudio.click();
//            else if (k === 'any') fileAny.click();
//            else if (k === 'camera') fileCamera.click();
//            else if (k === 'link') {
//                const url = prompt('Paste a link to share:');
//                if (url && url.trim()) addLinkBubble(url.trim());
//            }
//            popAtt.classList.remove('show');
//        });

//        fileImage?.addEventListener('change', () => handleFiles(fileImage.files, 'image'));
//        fileCamera?.addEventListener('change', () => handleFiles(fileCamera.files, 'image'));
//        fileDoc?.addEventListener('change', () => handleFiles(fileDoc.files, 'doc'));
//        fileVideo?.addEventListener('change', () => handleFiles(fileVideo.files, 'video'));
//        fileAudio?.addEventListener('change', () => handleFiles(fileAudio.files, 'audio'));
//        fileAny?.addEventListener('change', () => handleFiles(fileAny.files, 'any'));

//        function handleFiles(files, kind) {
//            if (!files || !files.length) return;
//            [...files].forEach(f => {
//                const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
//                const row = document.createElement('div');
//                row.className = 'msgrow me';
//                if (kind === 'image') {
//                    const reader = new FileReader();
//                    reader.onload = () => {
//                        row.innerHTML =
//                            `<div class="bubble">
//                 <div class="imgwrap"><img class="imgbubble" src="${reader.result}" alt=""></div>
//                 <div class="filecard" style="margin-top:6px">
//                   <svg viewBox='0 0 24 24' width='18' height='18' fill='none' stroke='currentColor' stroke-width='2'>
//                     <rect x='3' y='3' width='18' height='18' rx='2'/>
//                     <circle cx='8.5' cy='8.5' r='1.5'/>
//                     <path d='M21 15l-5-5L5 21'/>
//                   </svg>
//                   <div><div class='fname'>${escapeHtml(f.name)}</div><div class='fsize'>${humanSize(f.size)}</div></div>
//                 </div>
//                 <span class="time">${time}</span>
//               </div>`;
//                        cb.appendChild(row); cb.scrollTop = cb.scrollHeight;
//                    };
//                    reader.readAsDataURL(f);
//                } else {
//                    const icon = (kind === 'video')
//                        ? `<path d="M23 7l-7 5 7 5V7z"/><rect x="1" y="5" width="15" height="14" rx="2" ry="2"/>`
//                        : (kind === 'audio')
//                            ? `<path d="M9 18V5l12-2v13"/><circle cx="6" cy="18" r="3"/>`
//                            : `<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6"/>`;
//                    row.innerHTML =
//                        `<div class="bubble">
//               <div class="filecard">
//                 <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">${icon}</svg>
//                 <div><div class="fname">${escapeHtml(f.name)}</div><div class="fsize">${humanSize(f.size)}</div></div>
//               </div>
//               <span class="time">${time}</span>
//             </div>`;
//                    cb.appendChild(row); cb.scrollTop = cb.scrollHeight;
//                }
//            });
//            try { files.value = ''; } catch (_) { }
//        }

//        function addLinkBubble(url) {
//            const time = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
//            const row = document.createElement('div');
//            row.className = 'msgrow me';
//            const safe = escapeHtml(url);
//            row.innerHTML = `<div class="bubble"><div><strong>Link:</strong> <a href="${safe}" target="_blank" rel="noopener">${safe}</a></div><span class="time">${time}</span></div>`;
//            cb.appendChild(row); cb.scrollTop = cb.scrollHeight;
//        }

//        // utils
//        function humanSize(bytes) {
//            if (!bytes && bytes !== 0) return '';
//            const u = ['B', 'KB', 'MB', 'GB', 'TB']; let i = 0, n = bytes;
//            while (n >= 1024 && i < u.length - 1) { n /= 1024; i++; }
//            return `${n.toFixed(n < 10 && i > 0 ? 1 : 0)} ${u[i]}`;
//        }
//        function escapeHtml(s) {
//            return s.replace(/[&<>"']/g, m => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m]));
//        }
//    }

//    // expose
//    window.Tutorly = {
//        initTheme,
//        toggleTheme,
//        wireNav,
//        wireActions,
//        initProfilePack,
//        wireMessages
//    };

//    // auto-init common bits on load
//    document.addEventListener('DOMContentLoaded', () => {
//        initTheme();
//        wireNav();
//        wireActions();
//        initProfilePack();
//        // Only wire messages if the messages UI is present
//        if (document.getElementById('chatBody')) wireMessages();
//    });
//})();
