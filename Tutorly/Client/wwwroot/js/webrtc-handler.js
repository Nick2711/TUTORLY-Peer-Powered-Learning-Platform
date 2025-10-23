/**
 * WebRTC Handler - JavaScript Interop for Blazor
 * Manages WebRTC peer connections, media streams, and screen sharing
 */

window.webrtcHandler = (function () {
    // Store peer connections and streams
    const peerConnections = new Map();
    const remoteStreams = new Map();
    let localStream = null;
    let pendingIceCandidates = null; // Store ICE candidates until remote description is set
    let pendingOffers = new Map(); // Store offers that arrive before local stream is ready
    let screenStream = null;
    let dotNetReference = null;

    // ICE configuration with Metered TURN servers
    let iceConfiguration = {
        iceServers: [
            {
                urls: "stun:stun.relay.metered.ca:80",
            },
            {
                urls: "turn:standard.relay.metered.ca:80",
                username: "3741f15a4ef3d56614b1b6eb",
                credential: "SeXU2KPr8TQagQWm",
            },
            {
                urls: "turn:standard.relay.metered.ca:80?transport=tcp",
                username: "3741f15a4ef3d56614b1b6eb",
                credential: "SeXU2KPr8TQagQWm",
            },
            {
                urls: "turn:standard.relay.metered.ca:443",
                username: "3741f15a4ef3d56614b1b6eb",
                credential: "SeXU2KPr8TQagQWm",
            },
            {
                urls: "turns:standard.relay.metered.ca:443?transport=tcp",
                username: "3741f15a4ef3d56614b1b6eb",
                credential: "SeXU2KPr8TQagQWm",
            },
        ],
        iceCandidatePoolSize: 10
    };

    /**
     * Load TURN server credentials from Metered API
     */
    async function loadTurnCredentials() {
        try {
            console.log('🔑 Loading TURN credentials from Metered API...');
            // Use Metered API to get TURN credentials
            const response = await fetch("https://tutorly-rtc.metered.live/api/v1/turn/credentials?apiKey=3b32bc1254cde7f937d19a1f5f851950d4c9");

            if (response.ok) {
                const iceServers = await response.json();
                iceConfiguration.iceServers = iceServers;
                console.log('✅ TURN credentials loaded successfully from Metered API:', iceServers);
            } else {
                console.warn('⚠️ Failed to load TURN credentials from Metered API, using fallback STUN servers');
            }
        } catch (error) {
            console.warn('⚠️ Error loading TURN credentials from Metered API:', error);
        }
    }

    /**
     * Initialize local media stream (camera + microphone)
     */
    async function initializeMediaStream(videoElementId, audio = true, video = true) {
        try {
            console.log('🎥 ===== INITIALIZING MEDIA STREAM =====');
            console.log('🎥 Parameters:', { videoElementId, audio, video });
            console.log('🎥 Current localStream:', localStream);
            console.log('🎥 Current peerConnections count:', peerConnections.size);
            console.log('🎥 Current remoteStreams count:', remoteStreams.size);
            console.log('🎥 Current pendingOffers count:', pendingOffers.size);
            console.log('🎥 DotNet reference available:', !!dotNetReference);

            // Load TURN credentials first
            console.log('🎥 Loading TURN credentials...');
            await loadTurnCredentials();
            console.log('🎥 TURN credentials loaded successfully');

            const constraints = {
                audio: audio ? {
                    echoCancellation: true,
                    noiseSuppression: true,
                    autoGainControl: true
                } : false,
                video: video ? {
                    width: { ideal: 1280 },
                    height: { ideal: 720 },
                    facingMode: 'user'
                } : false
            };

            console.log('🎥 Requesting media with constraints:', constraints);
            console.log('🎥 Available media devices:', await navigator.mediaDevices.enumerateDevices());

            localStream = await navigator.mediaDevices.getUserMedia(constraints);
            console.log('✅ Media stream obtained successfully!');
            console.log('🎥 Stream ID:', localStream.id);
            console.log('🎥 Stream active:', localStream.active);
            console.log('🎥 Stream tracks count:', localStream.getTracks().length);
            console.log('🎥 Stream tracks details:', localStream.getTracks().map(t => ({
                kind: t.kind,
                enabled: t.enabled,
                readyState: t.readyState,
                id: t.id,
                label: t.label,
                muted: t.muted
            })));

            // If video was requested but we want to start with it disabled, disable the video track
            if (video && localStream.getVideoTracks().length > 0) {
                const videoTrack = localStream.getVideoTracks()[0];
                videoTrack.enabled = false; // Start with video disabled
                console.log('🎥 Video track disabled by default (camera off)');
            }

            // Attach to video element if provided
            if (videoElementId) {
                console.log('🎥 ===== ATTACHING TO VIDEO ELEMENT =====');
                console.log('🎥 Looking for video element:', videoElementId);

                const videoElement = document.getElementById(videoElementId);
                console.log('🎥 Video element found:', videoElement);
                console.log('🎥 Video element details:', videoElement ? {
                    id: videoElement.id,
                    tagName: videoElement.tagName,
                    className: videoElement.className,
                    style: videoElement.style.cssText,
                    srcObject: videoElement.srcObject,
                    muted: videoElement.muted,
                    autoplay: videoElement.autoplay,
                    playsInline: videoElement.playsInline
                } : 'ELEMENT NOT FOUND');

                console.log('🎥 All video elements in DOM:', Array.from(document.querySelectorAll('video')).map(v => ({
                    id: v.id,
                    className: v.className,
                    srcObject: !!v.srcObject
                })));

                if (!videoElement) {
                    console.error('❌ Video element not found:', videoElementId);
                    console.log('🎥 Available elements with IDs:', Array.from(document.querySelectorAll('[id]')).map(el => el.id));
                    console.log('🎥 All elements in DOM:', Array.from(document.querySelectorAll('*')).map(el => el.tagName + (el.id ? '#' + el.id : '')).slice(0, 20));
                    throw new Error(`Video element '${videoElementId}' not found`);
                }

                console.log('🎥 Setting srcObject on video element...');
                videoElement.srcObject = localStream;
                videoElement.muted = true; // Mute local audio to prevent feedback
                console.log('✅ Stream attached to video element:', videoElementId);
                console.log('🎥 Video element after attachment:', {
                    srcObject: !!videoElement.srcObject,
                    srcObjectId: videoElement.srcObject?.id,
                    muted: videoElement.muted,
                    autoplay: videoElement.autoplay,
                    playsInline: videoElement.playsInline
                });
                console.log('🎥 Stream tracks after attachment:', localStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled, readyState: t.readyState })));

                // Force play the video
                const playPromise = videoElement.play();
                if (playPromise !== undefined) {
                    playPromise.then(() => {
                        console.log('✅ Video started playing');
                        console.log('🎥 Video element dimensions:', videoElement.videoWidth, 'x', videoElement.videoHeight);
                    }).catch(err => {
                        console.error('❌ Error playing video:', err);
                        // Try again after a short delay
                        setTimeout(() => {
                            videoElement.play().catch(e => {
                                console.warn('⚠️ Retry play failed:', e);
                            });
                        }, 1000);
                    });
                }

                // Add event listeners for debugging
                videoElement.addEventListener('loadedmetadata', () => {
                    console.log('🎥 Video metadata loaded:', videoElement.videoWidth, 'x', videoElement.videoHeight);
                    // Force dimensions if they're 0
                    if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                        console.warn('⚠️ Video dimensions are 0, forcing dimensions...');
                        videoElement.style.width = '200px';
                        videoElement.style.height = '200px';
                    }
                });

                videoElement.addEventListener('canplay', () => {
                    console.log('🎥 Video can play');
                    // Ensure video has proper dimensions
                    if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                        console.warn('⚠️ Video dimensions still 0 on canplay, forcing dimensions...');
                        videoElement.style.width = '200px';
                        videoElement.style.height = '200px';
                    }
                });

                videoElement.addEventListener('error', (e) => {
                    console.error('❌ Video error:', e);
                });

                videoElement.addEventListener('loadstart', () => {
                    console.log('🎥 Video load started');
                });
            }

            console.log('✅ Local media stream initialized successfully:', localStream.id);

            // Process any pending offers now that local stream is available
            processPendingOffers();

            // Start speaking detection
            startSpeakingDetection();

            return { success: true, streamId: localStream.id };
        } catch (error) {
            console.error('❌ Error initializing media stream:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Enable video (enable existing video track)
     */
    async function enableVideo() {
        try {
            console.log('📹 Enabling video...');

            if (!localStream) {
                console.log('⚠️ No local stream found, initializing with video...');
                await initializeMediaStream('localVideo', true, true);
                return;
            }

            // Get existing video track and enable it
            const videoTrack = localStream.getVideoTracks()[0];
            if (videoTrack) {
                if (videoTrack.enabled) {
                    console.log('✅ Video already enabled');
                    return;
                }

                videoTrack.enabled = true;
                console.log('✅ Video track enabled');

                // Update the local video element to show the video
                const localVideoElement = document.getElementById('localVideo');
                if (localVideoElement) {
                    console.log('📹 Updating local video element to show video...');

                    // Clear and set the video element's srcObject
                    localVideoElement.srcObject = null;

                    setTimeout(() => {
                        localVideoElement.srcObject = localStream;
                        localVideoElement.load();

                        localVideoElement.onloadedmetadata = () => {
                            console.log('📹 Video metadata loaded, starting playback...');
                            localVideoElement.play().catch(e => console.log('Video play error:', e));
                        };

                        console.log('✅ Local video element updated with enabled video track');
                    }, 50);
                }

                // Update all peer connections with enabled video track
                console.log(`📹 Updating ${peerConnections.size} peer connections with enabled video track`);

                for (const [userId, pc] of peerConnections) {
                    try {
                        // Check if video track is already being sent
                        const videoSender = pc.getSenders().find(s => s.track && s.track.kind === 'video');
                        if (videoSender) {
                            // Replace existing video track
                            videoSender.replaceTrack(videoTrack);
                            console.log(`✅ Video track replaced in peer connection for ${userId}`);
                        } else {
                            // Add new video track
                            pc.addTrack(videoTrack, localStream);
                            console.log(`✅ Video track added to peer connection for ${userId}`);
                        }

                        // Create new offer to renegotiate the connection
                        await createOfferForUser(userId);
                    } catch (error) {
                        console.error(`❌ Error updating peer connection for ${userId}:`, error);
                    }
                }

                console.log('✅ Video enabled successfully (camera light should be on)');
            } else {
                console.log('⚠️ No video track found in local stream');
            }

            return { success: true };
        } catch (error) {
            console.error('❌ Error enabling video:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Disable video (disable existing video track)
     */
    async function disableVideo() {
        try {
            console.log('📹 Disabling video...');

            if (!localStream) {
                console.log('⚠️ No local stream found');
                return { success: true };
            }

            // Get existing video track and disable it
            const videoTrack = localStream.getVideoTracks()[0];
            if (videoTrack) {
                if (!videoTrack.enabled) {
                    console.log('✅ Video already disabled');
                    return { success: true };
                }

                videoTrack.enabled = false;
                console.log('✅ Video track disabled (camera light should turn off)');

                // Update the local video element to hide the video
                const localVideoElement = document.getElementById('localVideo');
                if (localVideoElement) {
                    console.log('📹 Updating local video element to hide video...');

                    // Clear and set the video element's srcObject
                    localVideoElement.srcObject = null;

                    setTimeout(() => {
                        localVideoElement.srcObject = localStream;
                        localVideoElement.load();

                        localVideoElement.onloadedmetadata = () => {
                            console.log('📹 Video metadata loaded (audio only), starting playback...');
                            localVideoElement.play().catch(e => console.log('Video play error:', e));
                        };

                        console.log('✅ Local video element updated with disabled video track');
                    }, 50);
                }

                // Update all peer connections with disabled video track
                console.log(`📹 Updating ${peerConnections.size} peer connections to disable video`);

                for (const [userId, pc] of peerConnections) {
                    try {
                        // Check if video track is already being sent
                        const videoSender = pc.getSenders().find(s => s.track && s.track.kind === 'video');
                        if (videoSender) {
                            // Replace existing video track with disabled track
                            videoSender.replaceTrack(videoTrack);
                            console.log(`✅ Video track disabled in peer connection for ${userId}`);
                        } else {
                            // Add disabled video track
                            pc.addTrack(videoTrack, localStream);
                            console.log(`✅ Disabled video track added to peer connection for ${userId}`);
                        }

                        // Create new offer to renegotiate the connection
                        await createOfferForUser(userId);
                    } catch (error) {
                        console.error(`❌ Error updating peer connection for ${userId}:`, error);
                    }
                }

                console.log('✅ Video disabled successfully (camera light should be off)');
            } else {
                console.log('⚠️ No video track found in local stream');
            }

            return { success: true };
        } catch (error) {
            console.error('❌ Error disabling video:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Create offer for a specific user (used for video toggle renegotiation)
     */
    async function createOfferForUser(userId) {
        try {
            const pc = peerConnections.get(userId);
            if (!pc) {
                console.log(`⚠️ No peer connection found for user ${userId}`);
                return;
            }

            console.log(`📤 Creating offer for user ${userId}...`);

            const offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });

            await pc.setLocalDescription(offer);
            console.log(`✅ Offer created and set for user ${userId}`);

            // Send the offer to the server
            if (window.dotNetReference) {
                await window.dotNetReference.invokeMethodAsync('OnOfferCreated', userId, offer.sdp, offer.type);
            }

            return offer;
        } catch (error) {
            console.error(`❌ Error creating offer for user ${userId}:`, error);
            throw error;
        }
    }

    /**
     * Start speaking detection (Discord-like feature)
     */
    function startSpeakingDetection() {
        if (!localStream) {
            console.warn('⚠️ No local stream available for speaking detection');
            return;
        }

        try {
            console.log('🎤 Starting speaking detection...');
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const analyser = audioContext.createAnalyser();
            const microphone = audioContext.createMediaStreamSource(localStream);

            microphone.connect(analyser);
            analyser.fftSize = 256;
            console.log('✅ Speaking detection initialized');

            const bufferLength = analyser.frequencyBinCount;
            const dataArray = new Uint8Array(bufferLength);

            function checkSpeaking() {
                analyser.getByteFrequencyData(dataArray);

                // Calculate average volume
                let sum = 0;
                for (let i = 0; i < bufferLength; i++) {
                    sum += dataArray[i];
                }
                const average = sum / bufferLength;

                // Threshold for speaking detection (adjust as needed)
                const isSpeaking = average > 30;

                // Update UI
                const localTile = document.querySelector('#localVideo')?.closest('.tile');
                if (localTile) {
                    if (isSpeaking) {
                        localTile.classList.add('speaking');
                    } else {
                        localTile.classList.remove('speaking');
                    }
                }

                requestAnimationFrame(checkSpeaking);
            }

            checkSpeaking();
        } catch (error) {
            console.warn('⚠️ Speaking detection not available:', error);
        }
    }

    /**
     * Stop local media stream
     */
    function stopLocalStream() {
        if (localStream) {
            console.log('🛑 Stopping local stream...');
            localStream.getTracks().forEach(track => track.stop());
            localStream = null;
            console.log('✅ Local stream stopped');
        } else {
            console.log('⚠️ No local stream to stop');
        }
    }

    /**
     * Add local stream tracks to all existing peer connections
     */
    function addLocalStreamToAllConnections() {
        if (!localStream) {
            console.warn('⚠️ No local stream available to add to connections');
            return { success: false, error: 'No local stream' };
        }

        try {
            console.log(`📹 Adding local stream to ${peerConnections.size} existing peer connections`);
            peerConnections.forEach((pc, userId) => {
                // Remove existing tracks first
                const senders = pc.getSenders();
                senders.forEach(sender => {
                    if (sender.track) {
                        pc.removeTrack(sender);
                    }
                });

                // Add new tracks
                localStream.getTracks().forEach(track => {
                    console.log(`📹 Adding ${track.kind} track to existing connection for ${userId}`);
                    pc.addTrack(track, localStream);
                });
            });

            console.log(`✅ Added local stream to ${peerConnections.size} peer connections`);
            return { success: true };
        } catch (error) {
            console.error('❌ Error adding local stream to connections:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Process pending offers that arrived before local stream was ready
     */
    async function processPendingOffers() {
        if (pendingOffers.size === 0) {
            console.log('📥 No pending offers to process');
            return;
        }

        console.log(`📥 Processing ${pendingOffers.size} pending offers`);

        for (const [userId, offerSdp] of pendingOffers) {
            try {
                console.log(`📥 Processing pending offer for ${userId}`);
                console.log(`📥 Retrieved offer SDP:`, offerSdp);
                const result = await createAnswer(userId, offerSdp);
                if (result.success) {
                    console.log(`✅ Successfully processed pending offer for ${userId}`);
                    // Notify Blazor about the answer
                    if (dotNetReference) {
                        await dotNetReference.invokeMethodAsync('TriggerAnswerCreatedAsync', userId, result.sdp);
                    }
                } else {
                    console.error(`❌ Failed to process pending offer for ${userId}:`, result.error);
                }
            } catch (error) {
                console.error(`❌ Error processing pending offer for ${userId}:`, error);
            }
        }

        // Clear processed offers
        pendingOffers.clear();
        console.log('✅ All pending offers processed');
    }

    /**
     * Create a peer connection for a specific user
     */
    function createPeerConnection(userId, isInitiator = false) {
        try {
            console.log('🔗 ===== CREATING PEER CONNECTION =====');
            console.log('🔗 Parameters:', { userId, isInitiator });
            console.log('🔗 Current peerConnections count:', peerConnections.size);
            console.log('🔗 Current localStream:', !!localStream);
            console.log('🔗 LocalStream tracks:', localStream ? localStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled })) : 'No local stream');
            console.log('🔗 ICE Configuration:', iceConfiguration);

            const pc = new RTCPeerConnection(iceConfiguration);
            peerConnections.set(userId, pc);
            console.log('✅ Peer connection created and stored for', userId);
            console.log('🔗 Peer connection state:', pc.connectionState);
            console.log('🔗 ICE connection state:', pc.iceConnectionState);
            console.log('🔗 Signaling state:', pc.signalingState);

            // Add local stream tracks to peer connection
            if (localStream) {
                console.log('📹 ===== ADDING LOCAL STREAM TRACKS =====');
                console.log('📹 Local stream tracks:', localStream.getTracks().map(t => ({
                    kind: t.kind,
                    enabled: t.enabled,
                    readyState: t.readyState,
                    id: t.id
                })));

                localStream.getTracks().forEach(track => {
                    console.log(`📹 Adding ${track.kind} track to peer connection for ${userId}`);
                    console.log('📹 Track details:', {
                        kind: track.kind,
                        enabled: track.enabled,
                        readyState: track.readyState,
                        id: track.id,
                        label: track.label
                    });
                    pc.addTrack(track, localStream);
                    console.log(`✅ ${track.kind} track added successfully`);
                });

                console.log('📹 Peer connection senders after adding tracks:', pc.getSenders().map(s => ({
                    track: s.track ? { kind: s.track.kind, enabled: s.track.enabled } : null,
                    dtmfSender: !!s.dtmf
                })));
            } else {
                console.warn(`⚠️ No local stream available when creating peer connection for ${userId}`);
            }

            // Handle incoming tracks
            pc.ontrack = (event) => {
                console.log('📹 ===== RECEIVED REMOTE TRACK =====');
                console.log('📹 Track details:', {
                    kind: event.track.kind,
                    enabled: event.track.enabled,
                    readyState: event.track.readyState,
                    id: event.track.id,
                    label: event.track.label,
                    muted: event.track.muted
                });
                console.log('📹 Event streams:', event.streams.map(s => ({ id: s.id, active: s.active, tracks: s.getTracks().length })));
                console.log('📹 From userId:', userId);

                if (!remoteStreams.has(userId)) {
                    console.log(`📹 Creating new remote stream for ${userId}`);
                    remoteStreams.set(userId, new MediaStream());
                }

                const remoteStream = remoteStreams.get(userId);
                console.log('📹 Remote stream before adding track:', {
                    id: remoteStream.id,
                    active: remoteStream.active,
                    tracks: remoteStream.getTracks().length
                });

                remoteStream.addTrack(event.track);
                console.log(`📹 Added ${event.track.kind} track to remote stream for ${userId}`);
                console.log('📹 Remote stream after adding track:', {
                    id: remoteStream.id,
                    active: remoteStream.active,
                    tracks: remoteStream.getTracks().length,
                    trackDetails: remoteStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled, readyState: t.readyState }))
                });

                // Automatically attach the remote stream to the video element
                console.log('📹 ===== ATTACHING REMOTE STREAM TO VIDEO ELEMENT =====');
                const videoElementId = `video_${userId}`;
                console.log('📹 Looking for video element:', videoElementId);

                const videoElement = document.getElementById(videoElementId);
                console.log('📹 Video element found:', !!videoElement);
                console.log('📹 Video element details:', videoElement ? {
                    id: videoElement.id,
                    className: videoElement.className,
                    srcObject: !!videoElement.srcObject,
                    muted: videoElement.muted,
                    autoplay: videoElement.autoplay,
                    playsInline: videoElement.playsInline
                } : 'ELEMENT NOT FOUND');

                console.log('📹 All video elements in DOM:', Array.from(document.querySelectorAll('video')).map(v => ({
                    id: v.id,
                    className: v.className,
                    srcObject: !!v.srcObject
                })));

                if (videoElement) {
                    console.log('📹 Setting srcObject on remote video element...');

                    // Clear and set the video element's srcObject to the remote stream
                    videoElement.srcObject = null;
                    setTimeout(() => {
                        videoElement.srcObject = remoteStream;
                        console.log(`✅ Remote stream automatically attached to ${videoElementId}`);
                    }, 50);

                    console.log('📹 Setting video attributes...');
                    // Set video attributes
                    videoElement.autoplay = true;
                    videoElement.playsInline = true;
                    videoElement.muted = false; // Allow audio for remote streams

                    console.log('📹 Applying video styling...');
                    // Force video to be visible with proper styling
                    videoElement.style.display = 'block';
                    videoElement.style.visibility = 'visible';
                    videoElement.style.opacity = '1';
                    videoElement.style.width = '100%';
                    videoElement.style.height = '100%';
                    videoElement.style.objectFit = 'cover';
                    videoElement.style.background = '#000';
                    videoElement.style.minHeight = '200px';
                    videoElement.style.minWidth = '200px';
                    videoElement.style.position = 'absolute';
                    videoElement.style.top = '0';
                    videoElement.style.left = '0';
                    videoElement.style.zIndex = '1';

                    console.log('📹 Video element after attachment:', {
                        srcObject: !!videoElement.srcObject,
                        srcObjectId: videoElement.srcObject?.id,
                        muted: videoElement.muted,
                        autoplay: videoElement.autoplay,
                        playsInline: videoElement.playsInline,
                        style: videoElement.style.cssText
                    });

                    // Add event listeners for debugging
                    videoElement.addEventListener('loadedmetadata', () => {
                        console.log(`📹 Remote video metadata loaded for ${userId}: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                        // Force dimensions if they're 0
                        if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                            console.warn(`⚠️ Remote video dimensions are 0 for ${userId}, forcing dimensions...`);
                            videoElement.style.width = '200px';
                            videoElement.style.height = '200px';
                        }
                    });

                    videoElement.addEventListener('canplay', () => {
                        console.log(`📹 Remote video can play for ${userId}`);
                        // Ensure video has proper dimensions
                        if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                            console.warn(`⚠️ Remote video dimensions still 0 on canplay for ${userId}, forcing dimensions...`);
                            videoElement.style.width = '200px';
                            videoElement.style.height = '200px';
                        }
                    });

                    videoElement.addEventListener('playing', () => {
                        console.log(`✅ Remote video started playing for ${userId}`);
                        console.log(`📹 Remote video dimensions: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                        // Final check for dimensions
                        if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                            console.warn(`⚠️ Remote video dimensions still 0 on playing for ${userId}, forcing dimensions...`);
                            videoElement.style.width = '200px';
                            videoElement.style.height = '200px';
                        }
                    });

                    videoElement.addEventListener('error', (e) => {
                        console.error(`❌ Remote video error for ${userId}:`, e);
                    });

                    // Force play the video with better error handling
                    const playPromise = videoElement.play();
                    if (playPromise !== undefined) {
                        playPromise.then(() => {
                            console.log(`✅ Remote video started playing for ${userId}`);
                            console.log(`📹 Remote video dimensions: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                        }).catch(err => {
                            console.error(`❌ Error playing remote video for ${userId}:`, err);
                            // Try multiple retry strategies
                            setTimeout(() => {
                                videoElement.load();
                                videoElement.play().catch(e => {
                                    console.warn(`⚠️ Retry 1 failed for ${userId}:`, e);
                                    // Try with different approach
                                    setTimeout(() => {
                                        videoElement.currentTime = 0;
                                        videoElement.play().catch(e2 => {
                                            console.warn(`⚠️ Retry 2 failed for ${userId}:`, e2);
                                        });
                                    }, 500);
                                });
                            }, 1000);
                        });
                    }
                } else {
                    console.warn(`⚠️ Video element not found for remote stream: ${videoElementId}`);
                }

                // Notify Blazor
                if (dotNetReference) {
                    console.log(`📤 Notifying Blazor about remote stream for ${userId}`);
                    dotNetReference.invokeMethodAsync('HandleRemoteStreamReceived', userId, event.track.kind);
                } else {
                    console.warn('❌ No DotNet reference available for remote stream notification');
                }
            };

            // Handle ICE candidates
            pc.onicecandidate = (event) => {
                if (event.candidate) {
                    console.log(`🔗 ICE candidate generated for ${userId}:`, event.candidate);
                    if (dotNetReference) {
                        console.log(`📤 Sending ICE candidate to Blazor for ${userId}`);
                        dotNetReference.invokeMethodAsync('HandleIceCandidate', userId, {
                            candidate: event.candidate.candidate,
                            sdpMid: event.candidate.sdpMid,
                            sdpMLineIndex: event.candidate.sdpMLineIndex
                        });
                    } else {
                        console.warn('❌ No DotNet reference available for ICE candidate');
                    }
                }
            };

            // Handle connection state changes
            pc.onconnectionstatechange = () => {
                console.log(`🔗 Connection state for ${userId}: ${pc.connectionState}`);
                if (dotNetReference) {
                    console.log(`📤 Notifying Blazor about connection state change for ${userId}`);
                    dotNetReference.invokeMethodAsync('HandleConnectionStateChanged', userId, pc.connectionState);
                } else {
                    console.warn('❌ No DotNet reference available for connection state notification');
                }
            };

            // Handle ICE connection state changes
            pc.oniceconnectionstatechange = () => {
                console.log(`🔗 ICE connection state for ${userId}: ${pc.iceConnectionState}`);
            };

            console.log(`✅ Peer connection setup complete for ${userId}`);
            return { success: true, message: 'Peer connection created' };
        } catch (error) {
            console.error('❌ Error creating peer connection:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Create an offer for a peer connection
     */
    async function createOffer(userId) {
        try {
            console.log('📤 ===== CREATING OFFER =====');
            console.log('📤 Parameters:', { userId });
            console.log('📤 Current peerConnections count:', peerConnections.size);
            console.log('📤 Current localStream:', !!localStream);
            console.log('📤 LocalStream tracks:', localStream ? localStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled })) : 'No local stream');

            const pc = peerConnections.get(userId);
            if (!pc) {
                console.error('❌ Peer connection not found for userId:', userId);
                console.log('📤 Available peer connections:', Array.from(peerConnections.keys()));
                throw new Error('Peer connection not found');
            }

            console.log('📤 Peer connection found:', {
                connectionState: pc.connectionState,
                iceConnectionState: pc.iceConnectionState,
                signalingState: pc.signalingState,
                senders: pc.getSenders().length,
                receivers: pc.getReceivers().length
            });

            console.log('📤 Creating offer with options...');
            const offer = await pc.createOffer({
                offerToReceiveAudio: true,
                offerToReceiveVideo: true
            });
            console.log('📤 Offer created:', {
                type: offer.type,
                sdp: offer.sdp ? offer.sdp.substring(0, 100) + '...' : 'No SDP'
            });

            console.log('📤 Setting local description...');
            await pc.setLocalDescription(offer);
            console.log(`✅ Offer created and set as local description for ${userId}`);
            console.log('📤 Local description set:', {
                type: pc.localDescription?.type,
                sdp: pc.localDescription?.sdp ? pc.localDescription.sdp.substring(0, 100) + '...' : 'No SDP'
            });

            return {
                success: true,
                sdp: offer.sdp,
                type: offer.type
            };
        } catch (error) {
            console.error('❌ Error creating offer:', error);
            console.error('❌ Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack
            });
            return { success: false, error: error.message };
        }
    }

    /**
     * Create an answer for a peer connection
     */
    async function createAnswer(userId, offerSdp) {
        try {
            console.log('📥 ===== CREATING ANSWER =====');
            console.log('📥 Parameters:', { userId, offerSdp: offerSdp ? offerSdp.substring(0, 100) + '...' : 'No SDP' });
            console.log('📥 Current peerConnections count:', peerConnections.size);
            console.log('📥 Current localStream:', !!localStream);
            console.log('📥 LocalStream tracks:', localStream ? localStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled })) : 'No local stream');
            console.log('📥 Current pendingOffers count:', pendingOffers.size);

            const pc = peerConnections.get(userId);
            if (!pc) {
                console.error('❌ Peer connection not found for userId:', userId);
                console.log('📥 Available peer connections:', Array.from(peerConnections.keys()));
                throw new Error('Peer connection not found');
            }

            console.log('📥 Peer connection found:', {
                connectionState: pc.connectionState,
                iceConnectionState: pc.iceConnectionState,
                signalingState: pc.signalingState,
                senders: pc.getSenders().length,
                receivers: pc.getReceivers().length,
                localDescription: pc.localDescription ? { type: pc.localDescription.type } : null,
                remoteDescription: pc.remoteDescription ? { type: pc.remoteDescription.type } : null
            });

            console.log(`📥 Creating answer for ${userId}`);
            console.log(`📥 Offer SDP received:`, offerSdp);

            // Wait for local stream if not available yet
            if (!localStream) {
                console.log(`⏳ ===== LOCAL STREAM NOT AVAILABLE - STORING AS PENDING =====`);
                console.log(`⏳ Local stream not available, storing offer as pending...`);
                console.log(`📥 Offer SDP to store:`, offerSdp);
                pendingOffers.set(userId, offerSdp);
                console.log(`📥 Stored pending offer for ${userId}, will process when local stream is ready`);
                console.log(`📥 Current pending offers:`, Array.from(pendingOffers.keys()));
                return { success: true, pending: true, message: 'Offer stored as pending' };
            }

            // Ensure local stream is added to peer connection if available
            if (localStream && pc.getSenders().length === 0) {
                console.log(`📹 ===== ADDING LOCAL STREAM TO PEER CONNECTION =====`);
                console.log(`📹 Adding local stream to peer connection for ${userId} before creating answer`);
                console.log(`📹 Local stream tracks:`, localStream.getTracks().map(t => ({ kind: t.kind, enabled: t.enabled })));
                localStream.getTracks().forEach(track => {
                    console.log(`📹 Adding ${track.kind} track to peer connection for ${userId}`);
                    pc.addTrack(track, localStream);
                });
                console.log(`📹 Peer connection senders after adding tracks:`, pc.getSenders().length);
            }

            // Validate offer SDP
            if (!offerSdp || typeof offerSdp !== 'string' || offerSdp.trim() === '') {
                console.error('❌ Invalid offer SDP:', offerSdp);
                throw new Error(`Invalid offer SDP: ${offerSdp}`);
            }

            console.log('📥 Setting remote description (offer)...');
            // Set remote description (offer)
            await pc.setRemoteDescription({
                type: 'offer',
                sdp: offerSdp
            });
            console.log('📥 Remote description set successfully');

            console.log('📥 Creating answer...');
            // Create answer
            const answer = await pc.createAnswer();
            console.log('📥 Answer created:', {
                type: answer.type,
                sdp: answer.sdp ? answer.sdp.substring(0, 100) + '...' : 'No SDP'
            });

            console.log('📥 Setting local description (answer)...');
            await pc.setLocalDescription(answer);
            console.log(`✅ Answer created and set as local description for ${userId}`);
            console.log('📥 Local description set:', {
                type: pc.localDescription?.type,
                sdp: pc.localDescription?.sdp ? pc.localDescription.sdp.substring(0, 100) + '...' : 'No SDP'
            });

            // Process any pending ICE candidates now that remote description is set
            if (pendingIceCandidates && pendingIceCandidates.has(userId)) {
                const candidates = pendingIceCandidates.get(userId);
                console.log(`🔄 Processing ${candidates.length} pending ICE candidates for ${userId}`);

                for (const candidateData of candidates) {
                    try {
                        const candidate = new RTCIceCandidate({
                            candidate: candidateData.candidate,
                            sdpMid: candidateData.sdpMid,
                            sdpMLineIndex: candidateData.sdpMLineIndex
                        });
                        await pc.addIceCandidate(candidate);
                        console.log(`✅ Pending ICE candidate added for ${userId}`);
                    } catch (error) {
                        console.error(`❌ Error adding pending ICE candidate for ${userId}:`, error);
                    }
                }

                // Clear processed candidates
                pendingIceCandidates.delete(userId);
            }

            return {
                success: true,
                sdp: answer.sdp,
                type: answer.type
            };
        } catch (error) {
            console.error('❌ Error creating answer:', error);
            console.error('❌ Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack,
                userId: userId,
                offerSdp: offerSdp
            });
            return { success: false, error: error.message };
        }
    }

    /**
     * Set remote description (answer)
     */
    async function setRemoteDescription(userId, answerSdp) {
        try {
            const pc = peerConnections.get(userId);
            if (!pc) {
                throw new Error('Peer connection not found');
            }

            console.log(`📥 Setting remote description for ${userId}:`, answerSdp);

            await pc.setRemoteDescription({
                type: 'answer',
                sdp: answerSdp
            });

            console.log(`✅ Remote description set for ${userId}`);

            // Process any pending ICE candidates now that remote description is set
            if (pendingIceCandidates && pendingIceCandidates.has(userId)) {
                const candidates = pendingIceCandidates.get(userId);
                console.log(`🔄 Processing ${candidates.length} pending ICE candidates for ${userId}`);

                for (const candidateData of candidates) {
                    try {
                        const candidate = new RTCIceCandidate({
                            candidate: candidateData.candidate,
                            sdpMid: candidateData.sdpMid,
                            sdpMLineIndex: candidateData.sdpMLineIndex
                        });
                        await pc.addIceCandidate(candidate);
                        console.log(`✅ Pending ICE candidate added for ${userId}`);
                    } catch (error) {
                        console.error(`❌ Error adding pending ICE candidate for ${userId}:`, error);
                    }
                }

                // Clear processed candidates
                pendingIceCandidates.delete(userId);
            }

            return { success: true };
        } catch (error) {
            console.error('❌ Error setting remote description:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Add ICE candidate to peer connection
     */
    async function addIceCandidate(userId, candidateData) {
        try {
            const pc = peerConnections.get(userId);
            if (!pc) {
                throw new Error('Peer connection not found');
            }

            console.log(`🔗 Adding ICE candidate for ${userId}:`, candidateData);

            // Check if remote description is set before adding ICE candidates
            if (!pc.remoteDescription) {
                console.warn(`⚠️ Remote description not set for ${userId}, storing ICE candidate for later`);
                // Store the candidate for later when remote description is set
                if (!pendingIceCandidates) {
                    pendingIceCandidates = new Map();
                }
                if (!pendingIceCandidates.has(userId)) {
                    pendingIceCandidates.set(userId, []);
                }
                pendingIceCandidates.get(userId).push(candidateData);
                return { success: true };
            }

            const candidate = new RTCIceCandidate({
                candidate: candidateData.candidate,
                sdpMid: candidateData.sdpMid,
                sdpMLineIndex: candidateData.sdpMLineIndex
            });

            await pc.addIceCandidate(candidate);
            console.log(`✅ ICE candidate added for ${userId}`);
            return { success: true };
        } catch (error) {
            console.error('❌ Error adding ICE candidate:', error);
            console.error('❌ Error details:', {
                name: error.name,
                message: error.message,
                stack: error.stack,
                candidateData: candidateData
            });
            return { success: false, error: error.message };
        }
    }

    /**
     * Attach remote stream to video element
     */
    function attachRemoteStream(userId, videoElementId) {
        try {
            console.log(`📹 Attaching remote stream for ${userId} to ${videoElementId}`);
            const stream = remoteStreams.get(userId);
            const videoElement = document.getElementById(videoElementId);

            if (!stream) {
                console.warn(`❌ No remote stream found for ${userId}`);
                return { success: false, error: 'Stream not found' };
            }

            if (!videoElement) {
                console.warn(`❌ Video element not found: ${videoElementId}`);
                return { success: false, error: 'Video element not found' };
            }

            // Clear any existing stream
            videoElement.srcObject = null;

            // Set the stream source with delay to ensure proper refresh
            setTimeout(() => {
                videoElement.srcObject = stream;
            }, 50);

            // Set video attributes
            videoElement.autoplay = true;
            videoElement.playsInline = true;
            videoElement.muted = false; // Allow audio for remote streams

            // Force video to be visible with proper styling
            videoElement.style.display = 'block';
            videoElement.style.visibility = 'visible';
            videoElement.style.opacity = '1';
            videoElement.style.width = '100%';
            videoElement.style.height = '100%';
            videoElement.style.objectFit = 'cover';
            videoElement.style.background = '#000';
            videoElement.style.minHeight = '200px';
            videoElement.style.minWidth = '200px';
            videoElement.style.position = 'absolute';
            videoElement.style.top = '0';
            videoElement.style.left = '0';
            videoElement.style.zIndex = '1';

            // Add event listeners for debugging
            videoElement.addEventListener('loadedmetadata', () => {
                console.log(`📹 Remote video metadata loaded for ${userId}: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                // Force dimensions if they're 0
                if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                    console.warn(`⚠️ Remote video dimensions are 0 for ${userId}, forcing dimensions...`);
                    videoElement.style.width = '200px';
                    videoElement.style.height = '200px';
                }
            });

            videoElement.addEventListener('canplay', () => {
                console.log(`📹 Remote video can play for ${userId}`);
                // Ensure video has proper dimensions
                if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                    console.warn(`⚠️ Remote video dimensions still 0 on canplay for ${userId}, forcing dimensions...`);
                    videoElement.style.width = '200px';
                    videoElement.style.height = '200px';
                }
            });

            videoElement.addEventListener('playing', () => {
                console.log(`✅ Remote video started playing for ${userId}`);
                console.log(`📹 Remote video dimensions: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                // Final check for dimensions
                if (videoElement.videoWidth === 0 || videoElement.videoHeight === 0) {
                    console.warn(`⚠️ Remote video dimensions still 0 on playing for ${userId}, forcing dimensions...`);
                    videoElement.style.width = '200px';
                    videoElement.style.height = '200px';
                }
            });

            videoElement.addEventListener('error', (e) => {
                console.error(`❌ Remote video error for ${userId}:`, e);
            });

            // Force play the video with better error handling
            const playPromise = videoElement.play();
            if (playPromise !== undefined) {
                playPromise.then(() => {
                    console.log(`✅ Remote video started playing for ${userId}`);
                    console.log(`📹 Remote video dimensions: ${videoElement.videoWidth}x${videoElement.videoHeight}`);
                }).catch(err => {
                    console.error(`❌ Error playing remote video for ${userId}:`, err);
                    // Try multiple retry strategies
                    setTimeout(() => {
                        videoElement.load();
                        videoElement.play().catch(e => {
                            console.warn(`⚠️ Retry 1 failed for ${userId}:`, e);
                            // Try with different approach
                            setTimeout(() => {
                                videoElement.currentTime = 0;
                                videoElement.play().catch(e2 => {
                                    console.warn(`⚠️ Retry 2 failed for ${userId}:`, e2);
                                });
                            }, 500);
                        });
                    }, 1000);
                });
            }

            console.log(`✅ Remote stream attached for ${userId} to ${videoElementId}`);
            return { success: true };
        } catch (error) {
            console.error('Error attaching remote stream:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Start screen sharing
     */
    async function startScreenShare() {
        try {
            console.log('🖥️ Starting screen share...');

            const constraints = {
                video: {
                    cursor: 'always',
                    displaySurface: 'monitor',
                    width: { ideal: 1920 },
                    height: { ideal: 1080 }
                },
                audio: true
            };

            screenStream = await navigator.mediaDevices.getDisplayMedia(constraints);
            console.log('✅ Screen share stream obtained:', screenStream.id);

            // Attach screen stream to screen share video element
            const screenShareElement = document.getElementById('screenShareVideo');
            if (screenShareElement) {
                screenShareElement.srcObject = screenStream;
                screenShareElement.muted = true; // Mute local audio to prevent feedback
                console.log('✅ Screen share stream attached to video element');

                // Force play the video
                const playPromise = screenShareElement.play();
                if (playPromise !== undefined) {
                    playPromise.then(() => {
                        console.log('✅ Screen share video started playing');
                        console.log('🖥️ Screen share video dimensions:', screenShareElement.videoWidth, 'x', screenShareElement.videoHeight);
                    }).catch(err => {
                        console.error('❌ Error playing screen share video:', err);
                    });
                }

                console.log('✅ Screen share attached to video element');
            } else {
                console.warn('⚠️ Screen share video element not found, but screen share started');
            }

            // Replace video track in all peer connections
            const screenTrack = screenStream.getVideoTracks()[0];
            console.log('🖥️ Replacing video tracks in peer connections with screen share track');

            peerConnections.forEach((pc, userId) => {
                const sender = pc.getSenders().find(s => s.track && s.track.kind === 'video');
                if (sender) {
                    sender.replaceTrack(screenTrack);
                    console.log(`✅ Screen track replaced for ${userId}`);
                } else {
                    console.warn(`⚠️ No video sender found for ${userId}`);
                }
            });

            // Handle screen share stop
            screenTrack.onended = () => {
                console.log('🖥️ Screen share ended by user');
                stopScreenShare();
                if (dotNetReference) {
                    console.log('📤 Notifying Blazor about screen share stop');
                    dotNetReference.invokeMethodAsync('HandleScreenShareStopped');
                }
            };

            console.log('✅ Screen share started successfully');
            return { success: true, streamId: screenStream.id };
        } catch (error) {
            console.error('❌ Error starting screen share:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Stop screen sharing
     */
    function stopScreenShare() {
        try {
            if (!screenStream) {
                console.log('⚠️ No screen stream to stop');
                return { success: true };
            }

            console.log('🖥️ Stopping screen share...');

            // Stop screen stream
            screenStream.getTracks().forEach(track => track.stop());
            console.log('✅ Screen stream tracks stopped');

            // Restore camera track in all peer connections
            if (localStream) {
                const videoTrack = localStream.getVideoTracks()[0];
                console.log('📹 Restoring camera tracks in peer connections');

                peerConnections.forEach((pc, userId) => {
                    const sender = pc.getSenders().find(s => s.track && s.track.kind === 'video');
                    if (sender && videoTrack) {
                        sender.replaceTrack(videoTrack);
                        console.log(`✅ Camera track restored for ${userId}`);
                    } else {
                        console.warn(`⚠️ No video sender or track found for ${userId}`);
                    }
                });
            } else {
                console.warn('⚠️ No local stream available to restore camera track');
            }

            screenStream = null;
            console.log('✅ Screen share stopped');
            return { success: true };
        } catch (error) {
            console.error('❌ Error stopping screen share:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Toggle audio track (mute/unmute)
     */
    function toggleAudio(enabled) {
        try {
            if (!localStream) {
                console.warn('⚠️ No local stream available for audio toggle');
                return { success: false, error: 'No local stream' };
            }

            localStream.getAudioTracks().forEach(track => {
                track.enabled = enabled;
            });

            console.log(`🎤 Audio ${enabled ? 'unmuted' : 'muted'}`);
            return { success: true, enabled: enabled };
        } catch (error) {
            console.error('❌ Error toggling audio:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Toggle video track (camera on/off)
     */
    function toggleVideo(enabled) {
        try {
            if (!localStream) {
                console.warn('⚠️ No local stream available for video toggle');
                return { success: false, error: 'No local stream' };
            }

            localStream.getVideoTracks().forEach(track => {
                track.enabled = enabled;
            });

            console.log(`📹 Video ${enabled ? 'enabled' : 'disabled'}`);
            return { success: true, enabled: enabled };
        } catch (error) {
            console.error('❌ Error toggling video:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Close peer connection
     */
    function closePeerConnection(userId) {
        try {
            console.log(`🔗 Closing peer connection for ${userId}`);
            const pc = peerConnections.get(userId);
            if (pc) {
                pc.close();
                peerConnections.delete(userId);
                console.log(`✅ Peer connection closed for ${userId}`);
            } else {
                console.warn(`⚠️ No peer connection found for ${userId}`);
            }

            const stream = remoteStreams.get(userId);
            if (stream) {
                stream.getTracks().forEach(track => track.stop());
                remoteStreams.delete(userId);
                console.log(`✅ Remote stream closed for ${userId}`);
            } else {
                console.warn(`⚠️ No remote stream found for ${userId}`);
            }

            return { success: true };
        } catch (error) {
            console.error('❌ Error closing peer connection:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Cleanup all connections and streams
     */
    function cleanup() {
        try {
            console.log('🧹 ===== COMPREHENSIVE WEBRTC CLEANUP =====');
            console.log('🧹 Current state before cleanup:');
            console.log('🧹   - Peer connections:', peerConnections.size);
            console.log('🧹   - Remote streams:', remoteStreams.size);
            console.log('🧹   - Pending offers:', pendingOffers.size);
            console.log('🧹   - Local stream:', !!localStream);
            console.log('🧹   - Screen stream:', !!screenStream);

            // Close all peer connections
            peerConnections.forEach((pc, userId) => {
                try {
                    console.log(`🔗 Closing peer connection for ${userId}...`);
                    console.log(`🔗   - Connection state: ${pc.connectionState}`);
                    console.log(`🔗   - ICE connection state: ${pc.iceConnectionState}`);
                    console.log(`🔗   - Signaling state: ${pc.signalingState}`);

                    // Close the connection
                    pc.close();
                    console.log(`✅ Peer connection closed for ${userId}`);
                } catch (error) {
                    console.error(`❌ Error closing peer connection for ${userId}:`, error);
                }
            });
            peerConnections.clear();
            console.log('✅ All peer connections cleared');

            // Stop all remote streams
            remoteStreams.forEach((stream, userId) => {
                try {
                    console.log(`📹 Stopping remote stream for ${userId}...`);
                    stream.getTracks().forEach(track => {
                        console.log(`📹   - Stopping ${track.kind} track: ${track.id}`);
                        track.stop();
                    });
                    console.log(`✅ Remote stream stopped for ${userId}`);
                } catch (error) {
                    console.error(`❌ Error stopping remote stream for ${userId}:`, error);
                }
            });
            remoteStreams.clear();
            console.log('✅ All remote streams cleared');

            // Clear pending offers
            if (pendingOffers) {
                console.log(`📥 Clearing ${pendingOffers.size} pending offers...`);
                pendingOffers.clear();
                console.log('✅ Pending offers cleared');
            }

            // Clear pending ICE candidates
            if (pendingIceCandidates) {
                console.log(`🔗 Clearing pending ICE candidates...`);
                pendingIceCandidates.clear();
                console.log('✅ Pending ICE candidates cleared');
            }

            // Stop local stream
            if (localStream) {
                console.log('🎥 Stopping local stream...');
                localStream.getTracks().forEach(track => {
                    console.log(`🎥   - Stopping ${track.kind} track: ${track.id}`);
                    track.stop();
                });
                localStream = null;
                console.log('✅ Local stream stopped and cleared');
            }

            // Stop screen stream
            if (screenStream) {
                console.log('🖥️ Stopping screen stream...');
                screenStream.getTracks().forEach(track => {
                    console.log(`🖥️   - Stopping ${track.kind} track: ${track.id}`);
                    track.stop();
                });
                screenStream = null;
                console.log('✅ Screen stream stopped and cleared');
            }

            console.log('✅ ===== WEBRTC CLEANUP COMPLETE =====');
            console.log('✅ Final state after cleanup:');
            console.log('✅   - Peer connections:', peerConnections.size);
            console.log('✅   - Remote streams:', remoteStreams.size);
            console.log('✅   - Pending offers:', pendingOffers.size);
            console.log('✅   - Local stream:', !!localStream);
            console.log('✅   - Screen stream:', !!screenStream);

            return { success: true };
        } catch (error) {
            console.error('❌ Error during cleanup:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Reset WebRTC state for new call (partial cleanup)
     */
    function resetForNewCall() {
        try {
            console.log('🔄 ===== RESETTING WEBRTC STATE FOR NEW CALL =====');
            console.log('🔄 Current state before reset:');
            console.log('🔄   - Peer connections:', peerConnections.size);
            console.log('🔄   - Remote streams:', remoteStreams.size);
            console.log('🔄   - Pending offers:', pendingOffers.size);

            // Close all existing peer connections
            peerConnections.forEach((pc, userId) => {
                try {
                    console.log(`🔗 Closing existing peer connection for ${userId}...`);
                    pc.close();
                } catch (error) {
                    console.error(`❌ Error closing peer connection for ${userId}:`, error);
                }
            });
            peerConnections.clear();

            // Stop all remote streams
            remoteStreams.forEach((stream, userId) => {
                try {
                    console.log(`📹 Stopping existing remote stream for ${userId}...`);
                    stream.getTracks().forEach(track => track.stop());
                } catch (error) {
                    console.error(`❌ Error stopping remote stream for ${userId}:`, error);
                }
            });
            remoteStreams.clear();

            // Clear pending offers and ICE candidates
            if (pendingOffers) {
                console.log(`📥 Clearing ${pendingOffers.size} pending offers...`);
                pendingOffers.clear();
            }

            if (pendingIceCandidates) {
                console.log(`🔗 Clearing pending ICE candidates...`);
                pendingIceCandidates.clear();
            }

            // Keep local stream and screen stream for reuse
            console.log('✅ ===== WEBRTC STATE RESET COMPLETE =====');
            console.log('✅ Final state after reset:');
            console.log('✅   - Peer connections:', peerConnections.size);
            console.log('✅   - Remote streams:', remoteStreams.size);
            console.log('✅   - Pending offers:', pendingOffers.size);
            console.log('✅   - Local stream preserved:', !!localStream);
            console.log('✅   - Screen stream preserved:', !!screenStream);

            return { success: true };
        } catch (error) {
            console.error('❌ Error during reset:', error);
            return { success: false, error: error.message };
        }
    }

    /**
     * Set .NET reference for callbacks
     */
    function setDotNetReference(dotNetRef) {
        dotNetReference = dotNetRef;
        console.log('✅ .NET reference set for WebRTC callbacks');
    }

    /**
     * Get connection statistics
     */
    async function getConnectionStats(userId) {
        try {
            console.log(`📊 Getting connection stats for ${userId}`);
            const pc = peerConnections.get(userId);
            if (!pc) {
                console.warn(`❌ Peer connection not found for ${userId}`);
                return { success: false, error: 'Peer connection not found' };
            }

            const stats = await pc.getStats();
            const statsObject = {};

            stats.forEach((report) => {
                if (report.type === 'inbound-rtp' || report.type === 'outbound-rtp') {
                    statsObject[report.type] = {
                        bytesReceived: report.bytesReceived,
                        bytesSent: report.bytesSent,
                        packetsLost: report.packetsLost,
                        jitter: report.jitter
                    };
                }
            });

            console.log(`✅ Connection stats retrieved for ${userId}:`, statsObject);
            return { success: true, stats: statsObject };
        } catch (error) {
            console.error('❌ Error getting connection stats:', error);
            return { success: false, error: error.message };
        }
    }

    // Public API
    // Fullscreen functions
    function enterFullscreen(videoId) {
        console.log(`🖥️ Entering fullscreen for ${videoId}`);
        const videoElement = document.getElementById(videoId);
        if (videoElement) {
            if (videoElement.requestFullscreen) {
                videoElement.requestFullscreen();
                console.log(`✅ Fullscreen requested for ${videoId}`);
            } else if (videoElement.webkitRequestFullscreen) {
                videoElement.webkitRequestFullscreen();
                console.log(`✅ Webkit fullscreen requested for ${videoId}`);
            } else if (videoElement.msRequestFullscreen) {
                videoElement.msRequestFullscreen();
                console.log(`✅ MS fullscreen requested for ${videoId}`);
            }
        } else {
            console.warn(`❌ Video element not found for fullscreen: ${videoId}`);
        }
    }

    function exitFullscreen() {
        console.log('🖥️ Exiting fullscreen');
        if (document.exitFullscreen) {
            document.exitFullscreen();
            console.log('✅ Fullscreen exited');
        } else if (document.webkitExitFullscreen) {
            document.webkitExitFullscreen();
            console.log('✅ Webkit fullscreen exited');
        } else if (document.msExitFullscreen) {
            document.msExitFullscreen();
            console.log('✅ MS fullscreen exited');
        }
    }

    return {
        initializeMediaStream,
        stopLocalStream,
        createPeerConnection,
        createOffer,
        createAnswer,
        setRemoteDescription,
        addIceCandidate,
        attachRemoteStream,
        startScreenShare,
        stopScreenShare,
        toggleAudio,
        toggleVideo,
        enableVideo,
        disableVideo,
        closePeerConnection,
        cleanup,
        resetForNewCall,
        setDotNetReference,
        getConnectionStats,
        enterFullscreen,
        exitFullscreen,
        addLocalStreamToAllConnections
    };
})();

