"use strict";

const call = {};
const offerCandidates = [];
const answerCandidates = [];

const serversConfig = {
    iceServers: [
        {
            urls: ['stun:stun1.l.google.com:19302', 'stun:stun2.l.google.com:19302'],
        },
        {
            urls: ['turn:home.adammihajlovic.ca:3478'],
            username: 'webrtc',
            credential: 'R3al5t3El',
        }
    ],
    iceCandidatePoolSize: 10,
};

// Global State
const pc = new RTCPeerConnection(serversConfig);
const ws = new WebSocket('ws://home.adammihajlovic.ca:5000/signaling');
ws.binaryType = 'arraybuffer'; // Set binary type to receive ArrayBuffer

let localStream = null;
let remoteStream = null;

// HTML elements
const webcamButton = document.getElementById('webcamButton');
const webcamVideo = document.getElementById('webcamVideo');
const callButton = document.getElementById('callButton');
const callInput = document.getElementById('callInput');
const answerButton = document.getElementById('answerButton');
const remoteVideo = document.getElementById('remoteVideo');
const hangupButton = document.getElementById('hangupButton');

// 1. Setup media sources

webcamButton.onclick = async () => {
    // localStream = await navigator.mediaDevices.getUserMedia({ video: true, audio: true });
    remoteStream = new MediaStream();

    // Push tracks from local stream to peer connection
    // localStream.getTracks().forEach((track) => {
    //   pc.addTrack(track, localStream);
    // });

    // Pull tracks from remote stream, add to video stream
    pc.ontrack = (event) => {
        event.streams[0].getTracks().forEach((track) => {
            remoteStream.addTrack(track);
        });
    };

    // webcamVideo.srcObject = localStream;
    remoteVideo.srcObject = remoteStream;

    callButton.disabled = false;
    webcamButton.disabled = true;
};

// 2. Ready WebSocket for signaling
ws.onopen = () => {
    console.log('WebSocket connection established');
};

ws.onmessage = (event) => {
    console.log('WebSocket message received: ', event);
    const data = JSON.parse(event.data);
    switch (data.type) {
        case 'offer':
            console.log('Offer!!! ', data);
            call.offer = data;
            answerButton.disabled = false;
            break;
        case 'answer':
            console.log('Answer!!! ', data);
            break;
        case 'candidate':
            console.log('Candidate!!! ', data);
            break;
        default:
            console.log('What is this ??? ', data);
            break;
    }

    // TODO: if description is offer send anwser setLocalDescription, setRemoteDescription to description received.

    // Receive answer
    // const data = event.data;
    // if (!pc.currentRemoteDescription && data?.answer) {
    //   const answerDescription = new RTCSessionDescription(data.answer);
    //   pc.setRemoteDescription(answerDescription);
    // }

    // Receive candidate
    // if (change.type === 'added') {
    //   const candidate = new RTCIceCandidate(data);
    //   pc.addIceCandidate(candidate);
    // }

    // Receive offer
    // const data = event.data;
    // if (!pc.currentRemoteDescription && data?.offer) {
    //   const offerDescription = new RTCSessionDescription(data.offer);
    //   pc.setRemoteDescription(offerDescription);
    // }
};

ws.onclose = () => {
    console.log('WebSocket connection closed');
};

ws.onerror = (error) => {
    console.error('WebSocket error:', error);
};

// 3. Create an offer
callButton.onclick = async () => {
    // Get candidates for caller, push to array.
    pc.onicecandidate = (event) => {
        const candidate = event.candidate ? event.candidate.toJSON() : undefined;
        if (!candidate)
            return;

        offerCandidates.push(candidate);
        const msg = { type: 'candidate', data: candidate }
        ws.send(JSON.stringify(msg));
    };

    // Create offer
    const offerDescription = await pc.createOffer();
    await pc.setLocalDescription(offerDescription);

    const offer = {
        sdp: offerDescription.sdp,
        type: offerDescription.type,
    };

    call.offer = offer;
    ws.send(JSON.stringify(offer));

    hangupButton.disabled = false;
};

// 4. Answer the call with the unique ID: Auto-anwser (both connected and awaiting start)
answerButton.onclick = async () => {
    pc.onicecandidate = (event) => {
        const candidate = event.candidate ? event.candidate.toJSON() : undefined;
        if (!candidate)
            return;

        answerCandidates.push(candidate);
        const msg = { type: 'candidate', data: candidate }
        ws.send(JSON.stringify(msg));
    };

    const offerDescription = call.offer;
    await pc.setRemoteDescription(new RTCSessionDescription(offerDescription));

    const answerDescription = await pc.createAnswer();
    await pc.setLocalDescription(answerDescription);

    const answer = {
        type: answerDescription.type,
        sdp: answerDescription.sdp,
    };

    call.answer = answer;
    ws.send(JSON.stringify(answer));
};