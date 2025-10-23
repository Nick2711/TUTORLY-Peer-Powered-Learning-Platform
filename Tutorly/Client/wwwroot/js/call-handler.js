let isDragging = false;
let currentX;
let currentY;
let initialX;
let initialY;
let xOffset = 0;
let yOffset = 0;

function makeDraggable(elementId) {
    const element = document.getElementById(elementId);
    if (!element) return;

    element.addEventListener("mousedown", dragStart);
    document.addEventListener("mousemove", drag);
    document.addEventListener("mouseup", dragEnd);
}

function dragStart(e) {
    if (e.target.classList.contains('floating-call__header')) {
        initialX = e.clientX - xOffset;
        initialY = e.clientY - yOffset;
        isDragging = true;

        // Add visual feedback
        e.target.style.cursor = 'grabbing';
    }
}

function drag(e) {
    if (isDragging) {
        e.preventDefault();
        currentX = e.clientX - initialX;
        currentY = e.clientY - initialY;
        xOffset = currentX;
        yOffset = currentY;

        const element = document.getElementById('floatingCall');
        if (element) {
            element.style.transform = `translate(${currentX}px, ${currentY}px)`;
        }
    }
}

function dragEnd(e) {
    if (isDragging) {
        initialX = currentX;
        initialY = currentY;
        isDragging = false;

        // Reset cursor
        const header = document.querySelector('.floating-call__header');
        if (header) {
            header.style.cursor = 'grab';
        }
    }
}

// WebRTC control functions
function toggleAudio() {
    // This will be called from the floating call window
    // The actual implementation will be handled by the existing WebRTC service
    console.log('Toggle audio called from floating window');
}

function toggleVideo() {
    // This will be called from the floating call window
    // The actual implementation will be handled by the existing WebRTC service
    console.log('Toggle video called from floating window');
}

// Export functions for Blazor interop
window.makeDraggable = makeDraggable;
window.toggleAudio = toggleAudio;
window.toggleVideo = toggleVideo;
