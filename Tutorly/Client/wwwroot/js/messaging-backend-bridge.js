/**
 * Messaging Backend Bridge
 * Connects the existing Messages UI to the real backend API and SignalR
 * Maintains 100% of existing styling and structure
 */

console.log('[Bridge] messaging-backend-bridge.js is loading...');

// Don't recreate if already exists (prevents losing state on multiple loads)
if (!window.TutorlyMessagingBackend) {
    console.log('[Bridge] Creating NEW TutorlyMessagingBackend instance');

    window.TutorlyMessagingBackend = (function () {
        let dotnetRef = null;
        let currentConversationId = null;
        let currentUserId = null;

        console.log('[Bridge] TutorlyMessagingBackend object created');

        return {
            /**
             * Initialize with user ID
             * Note: DotNet reference is retrieved from window._messagesPageDotNetRef (set separately)
             */
            init: function (userId) {
                console.log('[Bridge] init() called with userId:', userId);
                // Get dotnetRef from window (set by Blazor separately, as DotNetObjectReference can't be passed as parameter)
                dotnetRef = window._messagesPageDotNetRef;
                currentUserId = userId;
                console.log('[Bridge] dotnetRef retrieved from window:', !!dotnetRef);
                console.log('[Bridge] Messaging Backend Bridge initialized successfully!');
            },

            /**
             * Show loading state in conversation list
             */
            showLoadingState: function () {
                const list = document.getElementById('threadList');
                if (!list) return;

                list.innerHTML = '<li class="ms-thread" style="padding: 20px; text-align: center; color: #666;"><i data-feather="loader" style="animation: spin 1s linear infinite;"></i> Loading conversations...</li>';
                if (typeof feather !== 'undefined') {
                    feather.replace();
                }
            },

            /**
             * Load conversations from backend and populate the thread list
             * @param {Array} conversations - Array of conversation objects
             * @param {string} userId - Optional user ID to use for matching (overrides currentUserId)
             */
            loadConversations: function (conversations, userId) {
                console.log('[Bridge.loadConversations] Called with conversations:', conversations);
                console.log('[Bridge.loadConversations] userId parameter:', userId);

                // Update currentUserId if provided
                if (userId) {
                    currentUserId = userId;
                    console.log('[Bridge.loadConversations] ✓ Updated currentUserId to:', currentUserId);
                }

                const list = document.getElementById('threadList');
                if (!list) {
                    console.log('[Bridge.loadConversations] ERROR: threadList element not found');
                    return;
                }

                // Clear existing content (including loading state)
                list.innerHTML = '';

                // If no conversations, show a message
                if (!conversations || conversations.length === 0) {
                    list.innerHTML = '<li class="ms-thread" style="padding: 20px; text-align: center; color: #666;">No conversations yet. Start a new chat!</li>';
                    console.log('[Bridge.loadConversations] No conversations to display');
                    return;
                }

                console.log(`[Bridge.loadConversations] Processing ${conversations.length} conversations...`);
                console.log(`[Bridge.loadConversations] Current User ID: ${currentUserId}`);

                // Create thread items from real data
                conversations.forEach(conv => {
                    console.log('[Bridge.loadConversations] Processing conversation:', conv);
                    const li = document.createElement('li');
                    li.className = 'ms-thread';
                    li.dataset.id = conv.conversationId;

                    // Determine conversation name, avatar, and participant info for search
                    let conversationName = 'Chat';
                    let avatarUrl = `https://i.pravatar.cc/64?img=${Math.floor(Math.random() * 70) + 1}`;
                    let participantRole = '';
                    let participantId = ''; // For searching by ID

                    console.log(`[Bridge] Conv ${conv.conversationId} - Type: ${conv.conversationType}, Participants:`, conv.participants);
                    console.log(`[Bridge] CurrentUserId for matching: "${currentUserId}"`);

                    // Check for direct conversation (ConversationType.Direct = 0 or "Direct")
                    const isDirect = conv.conversationType === 0 || conv.conversationType === 'Direct';

                    if (isDirect && conv.participants && conv.participants.length > 0) {
                        // Find the other participant (not current user)
                        console.log(`[Bridge] Looking for other participant (not ${currentUserId})...`);

                        let otherParticipant = null;

                        // First, try to find the other participant (not me)
                        if (currentUserId) {
                            otherParticipant = conv.participants.find(p => p.userId && p.userId !== currentUserId);
                            console.log(`[Bridge] Other participant (not me):`, otherParticipant);
                        }

                        // If no match or no currentUserId, find participant with a non-empty name
                        if (!otherParticipant) {
                            console.log('[Bridge] Looking for participant with a name...');
                            otherParticipant = conv.participants.find(p => p.fullName && p.fullName.trim() !== '');
                            console.log(`[Bridge] Participant with name:`, otherParticipant);
                        }

                        // Last resort: use first participant
                        if (!otherParticipant && conv.participants.length > 0) {
                            console.log('[Bridge] ⚠️ Using first participant as fallback');
                            otherParticipant = conv.participants[0];
                        }

                        if (otherParticipant) {
                            // Use fullName if available, otherwise use userId as fallback
                            conversationName = (otherParticipant.fullName && otherParticipant.fullName.trim() !== '')
                                ? otherParticipant.fullName
                                : (otherParticipant.userId || 'Unknown User');
                            avatarUrl = otherParticipant.avatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(conversationName)}&background=6366f1&color=fff`;
                            participantRole = otherParticipant.role || '';

                            // Extract the ID from the fullName if it's in "Student 601282" format, or from role-specific props
                            const idMatch = conversationName.match(/\d+$/);
                            if (idMatch) {
                                participantId = idMatch[0];
                            } else if (otherParticipant.studentId) {
                                participantId = otherParticipant.studentId.toString();
                            } else if (otherParticipant.tutorId) {
                                participantId = otherParticipant.tutorId.toString();
                            } else if (otherParticipant.adminId) {
                                participantId = otherParticipant.adminId.toString();
                            }

                            console.log(`[Bridge] ✓ Using participant: ${otherParticipant.userId}, name: "${conversationName}", ID: "${participantId}"`);
                        }
                    } else { // Group conversation
                        conversationName = conv.groupName || 'Group Chat';
                        avatarUrl = conv.groupAvatarUrl || `https://ui-avatars.com/api/?name=${encodeURIComponent(conversationName)}&background=6366f1&color=fff`;
                    }

                    console.log(`[Bridge] ✓ Final name: "${conversationName}", ID: "${participantId}"`);

                    li.dataset.name = conversationName;
                    li.dataset.participantId = participantId; // Store for search
                    li.dataset.participantRole = participantRole; // Store for future use
                    li.dataset.sub = conv.lastMessageAt ? formatTimeAgo(conv.lastMessageAt) : 'No messages';
                    li.dataset.avatar = avatarUrl;

                    // Get last message preview
                    const preview = conv.lastMessage?.content
                        ? truncate(conv.lastMessage.content, 30)
                        : 'No messages yet';

                    li.innerHTML = `
                    <button type="button" class="ms-thread__btn">
                        <span class="ms-avatar"><img src="${li.dataset.avatar}" alt=""></span>
                        <div>
                            <div class="ms-tname">${escapeHtml(li.dataset.name)}</div>
                            <div class="ms-tprev">${escapeHtml(preview)}</div>
                        </div>
                        <div class="ms-tmeta">${li.dataset.sub}</div>
                        ${conv.unreadCount > 0 ? `<span class="ms-badge">${conv.unreadCount}</span>` : ''}
                    </button>
                `;

                    list.appendChild(li);
                });

                console.log(`Loaded ${conversations.length} conversations from backend`);
            },

            /**
             * Show loading indicator in chat
             */
            showLoadingMessages: function () {
                const cb = document.getElementById('chatBody');
                if (!cb) return;

                cb.innerHTML = `
                <div style="display: flex; flex-direction: column; align-items: center; justify-content: center; padding: 40px; color: #666;">
                    <svg style="animation: spin 1s linear infinite; margin-bottom: 12px;" width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <path d="M21 12a9 9 0 1 1-6.219-8.56"/>
                    </svg>
                    <div style="font-size: 14px;">Loading messages...</div>
                </div>
                <style>
                    @keyframes spin {
                        from { transform: rotate(0deg); }
                        to { transform: rotate(360deg); }
                    }
                </style>
            `;
            },

            /**
             * Load messages for a conversation
             */
            loadMessages: async function (conversationId, messages) {
                const cb = document.getElementById('chatBody');
                if (!cb) return;

                currentConversationId = conversationId;

                // Hide empty state when loading messages
                const emptyState = document.getElementById('emptyState');
                if (emptyState) {
                    emptyState.style.display = 'none';
                }

                cb.innerHTML = '';

                if (!messages || messages.length === 0) {
                    cb.innerHTML = '<div class="chip-day">No messages yet. Start the conversation!</div>';
                    cb.scrollTop = cb.scrollHeight;
                    return;
                }

                // Group messages by date
                const grouped = groupMessagesByDate(messages);

                // Render each group
                for (const [date, msgs] of Object.entries(grouped)) {
                    cb.innerHTML += `<div class="chip-day">${date}</div>`;

                    msgs.forEach(msg => {
                        // Handle both camelCase and PascalCase (C# JSON serialization)
                        const senderId = msg.senderId || msg.SenderId;
                        const messageId = msg.messageId || msg.MessageId;
                        const content = msg.content || msg.Content;
                        const createdAt = msg.createdAt || msg.CreatedAt;

                        // Detailed logging for debugging sender ID comparison
                        console.log('[Bridge.loadMessages] 🔍 DEBUG Message:', messageId);
                        console.log('  - senderId (normalized):', senderId, 'type:', typeof senderId);
                        console.log('  - currentUserId:', currentUserId, 'type:', typeof currentUserId);

                        const isMe = senderId === currentUserId;
                        const time = formatTime(createdAt);
                        console.log('  - isMe:', isMe);

                        if (isMe) {
                            cb.innerHTML += `
                            <div class="msgrow me" data-message-id="${messageId}">
                                <div class="bubble">
                                    <div>${escapeHtml(content)}</div>
                                    <span class="time">${time}</span>
                                </div>
                            </div>
                        `;
                        } else {
                            cb.innerHTML += `
                            <div class="msgrow" data-message-id="${messageId}">
                                <span class="ms-avatar"><img src="https://i.pravatar.cc/64?img=${Math.floor(Math.random() * 70) + 1}" alt=""></span>
                                <div class="bubble">
                                    <div>${escapeHtml(content)}</div>
                                    <span class="time">${time}</span>
                                </div>
                            </div>
                        `;
                        }
                    });
                }

                cb.scrollTop = cb.scrollHeight;
            },

            /**
             * Add a new message to the current chat (real-time)
             */
            addNewMessage: function (message) {
                console.log('[Bridge.addNewMessage] Called with message:', message);
                console.log('[Bridge.addNewMessage] Message object keys:', Object.keys(message));
                console.log('[Bridge.addNewMessage] Current conversation:', currentConversationId, 'Message conversation:', message.conversationId || message.ConversationId);

                // Handle both camelCase and PascalCase (C# JSON serialization)
                const conversationId = message.conversationId || message.ConversationId;
                const senderId = message.senderId || message.SenderId;
                const messageId = message.messageId || message.MessageId;
                const content = message.content || message.Content;
                const createdAt = message.createdAt || message.CreatedAt;

                if (conversationId !== currentConversationId) {
                    console.log('[Bridge.addNewMessage] ✗ Message for different conversation, ignoring');
                    return;
                }

                const cb = document.getElementById('chatBody');
                if (!cb) {
                    console.error('[Bridge.addNewMessage] ✗ chatBody element not found!');
                    return;
                }

                // Detailed logging for debugging sender ID comparison
                console.log('[Bridge.addNewMessage] 🔍 DEBUG SenderId comparison:');
                console.log('  - senderId (normalized):', senderId, 'type:', typeof senderId);
                console.log('  - currentUserId:', currentUserId, 'type:', typeof currentUserId);
                console.log('  - Strict equality (===):', senderId === currentUserId);
                console.log('  - Loose equality (==):', senderId == currentUserId);

                const isMe = senderId === currentUserId;
                const time = formatTime(createdAt);
                console.log('[Bridge.addNewMessage] ✓ Rendering message - isMe:', isMe, 'time:', time);

                const msgHtml = isMe
                    ? `<div class="msgrow me" data-message-id="${messageId}">
                       <div class="bubble">
                           <div>${escapeHtml(content)}</div>
                           <span class="time">${time}</span>
                       </div>
                   </div>`
                    : `<div class="msgrow" data-message-id="${messageId}">
                       <span class="ms-avatar"><img src="https://i.pravatar.cc/64?img=${Math.floor(Math.random() * 70) + 1}" alt=""></span>
                       <div class="bubble">
                           <div>${escapeHtml(content)}</div>
                           <span class="time">${time}</span>
                       </div>
                   </div>`;

                cb.insertAdjacentHTML('beforeend', msgHtml);
                cb.scrollTop = cb.scrollHeight;
            },

            /**
             * Send a message via .NET/SignalR
             */
            sendMessage: async function (content) {
                console.log('[Bridge.sendMessage] Called with content:', content?.substring(0, 30));

                // Always fetch dotnetRef fresh from window (in case bridge was reused from previous load)
                const activeDotnetRef = window._messagesPageDotNetRef || dotnetRef;
                console.log('[Bridge.sendMessage] dotnetRef from window:', !!window._messagesPageDotNetRef, 'from closure:', !!dotnetRef, 'using:', !!activeDotnetRef);
                console.log('[Bridge.sendMessage] conversationId:', currentConversationId);

                if (!activeDotnetRef) {
                    console.error('[Bridge.sendMessage] ✗ dotnetRef is null! Bridge not initialized properly.');
                    console.error('[Bridge.sendMessage] window._messagesPageDotNetRef:', window._messagesPageDotNetRef);
                    return false;
                }

                if (!currentConversationId) {
                    console.error('[Bridge.sendMessage] ✗ No conversation selected!');
                    return false;
                }

                if (!content || !content.trim()) {
                    console.error('[Bridge.sendMessage] ✗ Empty message!');
                    return false;
                }

                try {
                    console.log('[Bridge.sendMessage] ✓ Calling Blazor SendMessageFromJS...');
                    await activeDotnetRef.invokeMethodAsync('SendMessageFromJS', currentConversationId, content);
                    console.log('[Bridge.sendMessage] ✓ Successfully called Blazor!');
                    return true;
                } catch (error) {
                    console.error('[Bridge.sendMessage] ✗ Error calling Blazor:', error);
                    return false;
                }
            },

            /**
             * Get current conversation ID
             */
            getCurrentConversationId: function () {
                return currentConversationId;
            },

            /**
             * Search/filter conversations by participant name or ID
             * @param {string} searchQuery - The search text
             */
            searchConversations: function (searchQuery) {
                console.log('[Bridge.searchConversations] Searching for:', searchQuery);

                const list = document.getElementById('threadList');
                if (!list) {
                    console.error('[Bridge.searchConversations] threadList not found');
                    return;
                }

                const threads = list.querySelectorAll('.ms-thread');
                const query = searchQuery.toLowerCase().trim();

                let visibleCount = 0;

                threads.forEach(thread => {
                    // Get participant name from visible text
                    const nameElement = thread.querySelector('.ms-tname') || thread.querySelector('.ms-thread__name');
                    const name = nameElement ? nameElement.textContent.toLowerCase() : '';

                    // Get participant ID from data attribute (stored when conversation was loaded)
                    const participantId = thread.dataset.participantId || '';

                    // Match if query is empty, OR matches name, OR matches ID
                    const matchesName = name.includes(query);
                    const matchesId = participantId.includes(query);

                    if (query === '' || matchesName || matchesId) {
                        thread.style.display = '';
                        visibleCount++;
                        console.log(`[Bridge.searchConversations] ✓ Match: "${name}" (ID: ${participantId})`);
                    } else {
                        thread.style.display = 'none';
                    }
                });

                console.log(`[Bridge.searchConversations] Query: "${searchQuery}", Visible: ${visibleCount}/${threads.length}`);

                // Show "no results" message if needed
                const existingNoResults = list.querySelector('.ms-no-results');
                if (existingNoResults) {
                    existingNoResults.remove();
                }

                if (visibleCount === 0 && threads.length > 0) {
                    const noResultsHtml = `
                    <li class="ms-no-results" style="padding: 20px; text-align: center; color: #666;">
                        No conversations found matching "${escapeHtml(searchQuery)}"
                    </li>
                `;
                    list.insertAdjacentHTML('beforeend', noResultsHtml);
                }
            },

            /**
             * Set current conversation ID (when user clicks a thread)
             */
            setCurrentConversationId: function (conversationId) {
                currentConversationId = conversationId;
            },

            /**
             * Search for users by name or ID
             */
            searchUsers: async function (query) {
                if (!dotnetRef || !query || query.trim().length < 2) return [];

                try {
                    const results = await dotnetRef.invokeMethodAsync('SearchUsers', query);
                    return results || [];
                } catch (error) {
                    console.error('Error searching users:', error);
                    return [];
                }
            },

            /**
             * Create a direct conversation with a user
             */
            createDirectConversation: async function (userId, userName) {
                if (!dotnetRef || !userId) return null;

                try {
                    const conversation = await dotnetRef.invokeMethodAsync('CreateDirectConversation', userId, userName);
                    if (conversation) {
                        // Reload all conversations to show the new one
                        await dotnetRef.invokeMethodAsync('LoadConversationMessages', conversation.conversationId);
                        return conversation;
                    }
                    return null;
                } catch (error) {
                    console.error('Error creating conversation:', error);
                    return null;
                }
            }
        };

        // Helper functions
        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function truncate(str, maxLen) {
            return str.length > maxLen ? str.substring(0, maxLen) + '…' : str;
        }

        function formatTimeAgo(dateString) {
            const date = new Date(dateString);
            const now = new Date();
            const diffMs = now - date;
            const diffMins = Math.floor(diffMs / 60000);
            const diffHours = Math.floor(diffMs / 3600000);
            const diffDays = Math.floor(diffMs / 86400000);

            if (diffMins < 1) return 'Just now';
            if (diffMins < 60) return `${diffMins}m ago`;
            if (diffHours < 24) return `${diffHours}h ago`;
            if (diffDays === 1) return 'Yesterday';
            if (diffDays < 7) return `${diffDays}d ago`;
            return date.toLocaleDateString();
        }

        function formatTime(dateString) {
            const date = new Date(dateString);
            return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        }

        function groupMessagesByDate(messages) {
            const grouped = {};
            messages.forEach(msg => {
                // Handle both camelCase and PascalCase
                const createdAt = msg.createdAt || msg.CreatedAt;
                const date = new Date(createdAt);
                const today = new Date();
                const yesterday = new Date(today);
                yesterday.setDate(yesterday.getDate() - 1);

                let dateKey;
                if (date.toDateString() === today.toDateString()) {
                    dateKey = 'Today';
                } else if (date.toDateString() === yesterday.toDateString()) {
                    dateKey = 'Yesterday';
                } else {
                    dateKey = date.toLocaleDateString();
                }

                if (!grouped[dateKey]) {
                    grouped[dateKey] = [];
                }
                grouped[dateKey].push(msg);
            });
            return grouped;
        }
    })();
} else {
    console.log('[Bridge] TutorlyMessagingBackend already exists - reusing existing instance');
}

