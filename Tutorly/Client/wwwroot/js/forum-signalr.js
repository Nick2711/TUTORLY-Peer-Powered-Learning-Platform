// SignalR connection for forum real-time updates
class ForumSignalR {
    constructor() {
        this.connection = null;
        this.isConnected = false;
    }

    async start() {
        try {
            // Create SignalR connection
            this.connection = new signalR.HubConnectionBuilder()
                .withUrl("/forumHub")
                .withAutomaticReconnect()
                .build();

            // Set up event handlers
            this.setupEventHandlers();

            // Start the connection
            await this.connection.start();
            this.isConnected = true;
            console.log("SignalR connected to forum hub");
        } catch (err) {
            console.error("Error starting SignalR connection:", err);
        }
    }

    setupEventHandlers() {
        // New post notification
        this.connection.on("NewPost", (post) => {
            console.log("New post received:", post);
            this.handleNewPost(post);
        });

        // New response notification
        this.connection.on("NewResponse", (response) => {
            console.log("New response received:", response);
            this.handleNewResponse(response);
        });

        // Vote update notification (for responses)
        this.connection.on("VoteUpdate", (data) => {
            console.log("Vote update received:", data);
            this.handleVoteUpdate(data);
        });

        // Post vote update notification
        this.connection.on("PostVoteUpdate", (data) => {
            console.log("Post vote update received:", data);
            this.handlePostVoteUpdate(data);
        });

        // Community update notification
        this.connection.on("CommunityUpdate", (community) => {
            console.log("Community update received:", community);
            this.handleCommunityUpdate(community);
        });

        // Post update notification
        this.connection.on("PostUpdate", (post) => {
            console.log("Post update received:", post);
            this.handlePostUpdate(post);
        });

        // Connection state handlers
        this.connection.onclose(() => {
            this.isConnected = false;
            console.log("SignalR connection closed");
        });

        this.connection.onreconnecting(() => {
            console.log("SignalR reconnecting...");
        });

        this.connection.onreconnected(() => {
            this.isConnected = true;
            console.log("SignalR reconnected");
        });
    }

    // Join a community group
    async joinCommunity(communityId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("JoinCommunity", communityId);
                console.log(`Joined community ${communityId}`);
            } catch (err) {
                console.error("Error joining community:", err);
            }
        }
    }

    // Leave a community group
    async leaveCommunity(communityId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("LeaveCommunity", communityId);
                console.log(`Left community ${communityId}`);
            } catch (err) {
                console.error("Error leaving community:", err);
            }
        }
    }

    // Join a thread group
    async joinThread(threadId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("JoinThread", threadId);
                console.log(`Joined thread ${threadId}`);
            } catch (err) {
                console.error("Error joining thread:", err);
            }
        }
    }

    // Leave a thread group
    async leaveThread(threadId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("LeaveThread", threadId);
                console.log(`Left thread ${threadId}`);
            } catch (err) {
                console.error("Error leaving thread:", err);
            }
        }
    }

    // Join a post group
    async joinPost(postId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("JoinPost", postId);
                console.log(`Joined post ${postId}`);
            } catch (err) {
                console.error("Error joining post:", err);
            }
        }
    }

    // Leave a post group
    async leavePost(postId) {
        if (this.isConnected) {
            try {
                await this.connection.invoke("LeavePost", postId);
                console.log(`Left post ${postId}`);
            } catch (err) {
                console.error("Error leaving post:", err);
            }
        }
    }

    // Event handlers for UI updates
    handleNewPost(post) {
        // Trigger a custom event that the UI can listen to
        window.dispatchEvent(new CustomEvent('forumNewPost', { detail: post }));
    }

    handleNewResponse(response) {
        // Trigger a custom event that the UI can listen to
        window.dispatchEvent(new CustomEvent('forumNewResponse', { detail: response }));
    }

    handleVoteUpdate(data) {
        // Update vote count in UI
        const voteElement = document.querySelector(`[data-response-id="${data.ResponseId}"] .vote-count`);
        if (voteElement) {
            voteElement.textContent = data.VoteCount;
        }

        // Trigger a custom event
        window.dispatchEvent(new CustomEvent('forumVoteUpdate', { detail: data }));
    }

    handlePostVoteUpdate(data) {
        console.log(`Handling post vote update:`, data);

        // C# sends PascalCase (PostId, VoteCount), JavaScript might receive camelCase
        const postId = data.PostId || data.postId;
        const voteCount = data.VoteCount || data.voteCount;

        console.log(`Post ${postId} now has ${voteCount} votes`);

        // Trigger a custom event that Blazor components can listen to
        window.dispatchEvent(new CustomEvent('postVoteUpdate', {
            detail: {
                postId: postId,
                voteCount: voteCount
            }
        }));
    }

    handleCommunityUpdate(community) {
        // Trigger a custom event that the UI can listen to
        window.dispatchEvent(new CustomEvent('forumCommunityUpdate', { detail: community }));
    }

    handlePostUpdate(post) {
        // Trigger a custom event that the UI can listen to
        window.dispatchEvent(new CustomEvent('forumPostUpdate', { detail: post }));
    }

    // Stop the connection
    async stop() {
        if (this.connection) {
            await this.connection.stop();
            this.isConnected = false;
            console.log("SignalR connection stopped");
        }
    }
}

// Global instance
window.forumSignalR = new ForumSignalR();

// Auto-start when page loads
document.addEventListener('DOMContentLoaded', () => {
    window.forumSignalR.start();
});
