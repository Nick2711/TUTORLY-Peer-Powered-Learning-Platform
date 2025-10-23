// Metered Video SDK JavaScript Interop
window.meteredVideoInterop = {
    // Check if Metered SDK is loaded
    isSDKLoaded: function () {
        return typeof Metered !== 'undefined' && typeof Metered.Meeting !== 'undefined';
    },

    // Wait for SDK to be loaded
    waitForSDK: function (timeout = 10000) {
        return new Promise((resolve, reject) => {
            if (this.isSDKLoaded()) {
                resolve(true);
                return;
            }

            const startTime = Date.now();
            const checkInterval = setInterval(() => {
                if (this.isSDKLoaded()) {
                    clearInterval(checkInterval);
                    resolve(true);
                } else if (Date.now() - startTime > timeout) {
                    clearInterval(checkInterval);
                    reject(new Error('Metered SDK failed to load within timeout'));
                }
            }, 100);
        });
    },

    // Create a new Metered meeting instance
    createMeeting: function () {
        if (!this.isSDKLoaded()) {
            throw new Error('Metered SDK not loaded');
        }
        return new Metered.Meeting();
    },

    // Join a meeting
    joinMeeting: async function (meeting, joinOptions) {
        try {
            console.log("Joining meeting with options:", joinOptions);
            const meetingInfo = await meeting.join(joinOptions);
            console.log("Meeting joined successfully:", meetingInfo);
            return meetingInfo;
        } catch (error) {
            console.error("Error joining meeting:", error);
            console.error("Error details:", {
                message: error.message,
                stack: error.stack,
                name: error.name
            });
            throw error;
        }
    },

    // Start video
    startVideo: async function (meeting) {
        try {
            await meeting.startVideo();
            console.log("Video started");
        } catch (error) {
            console.error("Error starting video:", error);
            throw error;
        }
    },

    // Stop video
    stopVideo: async function (meeting) {
        try {
            await meeting.stopVideo();
            console.log("Video stopped");
        } catch (error) {
            console.error("Error stopping video:", error);
            throw error;
        }
    },

    // Start audio
    startAudio: async function (meeting) {
        try {
            await meeting.startAudio();
            console.log("Audio started");
        } catch (error) {
            console.error("Error starting audio:", error);
            throw error;
        }
    },

    // Stop audio
    stopAudio: async function (meeting) {
        try {
            await meeting.stopAudio();
            console.log("Audio stopped");
        } catch (error) {
            console.error("Error stopping audio:", error);
            throw error;
        }
    },

    // Leave meeting
    leaveMeeting: async function (meeting) {
        try {
            await meeting.leave();
            console.log("Left meeting");
        } catch (error) {
            console.error("Error leaving meeting:", error);
            throw error;
        }
    },

    // Setup event handlers
    setupMeteredEventHandlers: function (meeting, dotNetRef) {
        console.log("Setting up Metered event handlers");

        // Participant joined event
        meeting.on("participantJoined", function (participantInfo) {
            console.log("Participant joined:", participantInfo);
            dotNetRef.invokeMethodAsync("OnParticipantJoinedJS", JSON.stringify(participantInfo));
        });

        // Participant left event
        meeting.on("participantLeft", function (participantInfo) {
            console.log("Participant left:", participantInfo);
            dotNetRef.invokeMethodAsync("OnParticipantLeftJS", JSON.stringify(participantInfo));
        });

        // Local track started event
        meeting.on("localTrackStarted", function (trackItem) {
            console.log("Local track started:", trackItem);

            if (trackItem.type === "video") {
                // Convert MediaStreamTrack to MediaStream for HTML video element
                var track = trackItem.track;
                var mediaStream = new MediaStream([track]);

                // Find local video element and set source
                var localVideo = document.getElementById("localVideo");
                if (localVideo) {
                    localVideo.srcObject = mediaStream;
                    localVideo.play();
                }
            }

            dotNetRef.invokeMethodAsync("OnLocalTrackStartedJS", JSON.stringify(trackItem));
        });

        // Remote track started event
        meeting.on("remoteTrackStarted", function (remoteTrackItem) {
            console.log("Remote track started:", remoteTrackItem);

            if (remoteTrackItem.type === "video") {
                // Convert MediaStreamTrack to MediaStream for HTML video element
                var remoteTrack = remoteTrackItem.track;
                var remoteStream = new MediaStream([remoteTrack]);

                // Create or find remote video element
                var remoteVideoId = `remoteVideo_${remoteTrackItem.participantSessionId}`;
                var remoteVideo = document.getElementById(remoteVideoId);

                if (!remoteVideo) {
                    // Create new video element for this participant
                    remoteVideo = document.createElement("video");
                    remoteVideo.id = remoteVideoId;
                    remoteVideo.autoplay = true;
                    remoteVideo.playsinline = true;
                    remoteVideo.className = "remote-video";

                    // Add to remote video container
                    var container = document.getElementById("remoteVideoContainer");
                    if (container) {
                        container.appendChild(remoteVideo);
                    }
                }

                remoteVideo.srcObject = remoteStream;
                remoteVideo.play();
            }

            dotNetRef.invokeMethodAsync("OnRemoteTrackStartedJS", JSON.stringify(remoteTrackItem));
        });

        // Remote track ended event
        meeting.on("remoteTrackEnded", function (remoteTrackItem) {
            console.log("Remote track ended:", remoteTrackItem);

            // Remove video element for this track
            var remoteVideoId = `remoteVideo_${remoteTrackItem.participantSessionId}`;
            var remoteVideo = document.getElementById(remoteVideoId);
            if (remoteVideo) {
                remoteVideo.remove();
            }
        });

        // Meeting ended event
        meeting.on("meetingEnded", function () {
            console.log("Meeting ended");
            dotNetRef.invokeMethodAsync("OnMeetingEndedJS");
        });
    },

    // Setup event handlers for Metered meeting
    setupMeteredEventHandlers: function (meeting, dotNetRef) {
        console.log("Setting up Metered event handlers");

        // Participant joined event
        meeting.on("participantJoined", function (participant) {
            console.log("Participant joined:", participant);
            dotNetRef.invokeMethodAsync("OnParticipantJoinedJS", JSON.stringify(participant));
        });

        // Participant left event
        meeting.on("participantLeft", function (participant) {
            console.log("Participant left:", participant);
            dotNetRef.invokeMethodAsync("OnParticipantLeftJS", JSON.stringify(participant));
        });

        // Local track started event
        meeting.on("localTrackStarted", function (localTrackItem) {
            console.log("Local track started:", localTrackItem);

            // Attach local video to video element
            if (localTrackItem.track && localTrackItem.track.kind === "video") {
                var localVideo = document.getElementById("localVideo");
                if (localVideo) {
                    localVideo.srcObject = localTrackItem.track;
                    localVideo.play();
                }
            }

            dotNetRef.invokeMethodAsync("OnLocalTrackStartedJS", JSON.stringify(localTrackItem));
        });

        // Remote track started event
        meeting.on("remoteTrackStarted", function (remoteTrackItem) {
            console.log("Remote track started:", remoteTrackItem);

            // Attach remote video to video element
            if (remoteTrackItem.track && remoteTrackItem.track.kind === "video") {
                var remoteVideoId = `remoteVideo_${remoteTrackItem.participantSessionId}`;
                var remoteVideo = document.getElementById(remoteVideoId);

                if (!remoteVideo) {
                    // Create new video element for this participant
                    remoteVideo = document.createElement("video");
                    remoteVideo.id = remoteVideoId;
                    remoteVideo.autoplay = true;
                    remoteVideo.playsinline = true;
                    remoteVideo.className = "remote-video";

                    // Add to remote video container
                    var container = document.getElementById("remoteVideoContainer");
                    if (container) {
                        container.appendChild(remoteVideo);
                    }
                }

                remoteVideo.srcObject = remoteTrackItem.track;
                remoteVideo.play();
            }

            dotNetRef.invokeMethodAsync("OnRemoteTrackStartedJS", JSON.stringify(remoteTrackItem));
        });

        // Remote track ended event
        meeting.on("remoteTrackEnded", function (remoteTrackItem) {
            console.log("Remote track ended:", remoteTrackItem);

            // Remove video element for this track
            var remoteVideoId = `remoteVideo_${remoteTrackItem.participantSessionId}`;
            var remoteVideo = document.getElementById(remoteVideoId);
            if (remoteVideo) {
                remoteVideo.remove();
            }
        });

        // Meeting ended event
        meeting.on("meetingEnded", function () {
            console.log("Meeting ended");
            dotNetRef.invokeMethodAsync("OnMeetingEndedJS");
        });
    }
};
