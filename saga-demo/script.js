// Global settings
let speedMultiplier = 1.0;
let isRunning = false;
let failMode = null; // null, 'billing', 'card'
let currentStepIdx = 0; // 0 to 5

// DOM elements
const elLog = document.getElementById('log-container');
const elLayer = document.getElementById('animation-layer');
const stLabel = document.getElementById('orch-state');
const steps = ['st-init', 'st-otp', 'st-pmt', 'st-bill', 'st-card', 'st-end'];
const txIdLabel = document.getElementById('tx-id');

// Set Playback Speed
function setSpeed(val, btnObj) {
    speedMultiplier = val;
    document.querySelectorAll('.spd-btn').forEach(b => b.classList.remove('active'));
    btnObj.classList.add('active');
}

// Ensure clean reset
function resetSim() {
    isRunning = false;
    failMode = null;
    currentStepIdx = 0;
    
    // UI resets
    document.getElementById('btn-start').disabled = false;
    document.getElementById('btn-fail-bill').disabled = false;
    document.getElementById('btn-fail-card').disabled = false;
    document.getElementById('btn-otp').classList.add('hidden');
    document.getElementById('srv-payment').querySelector('.orch-ring').classList.remove('orch-anim');
    
    stLabel.innerText = "STATE: IDLE";
    stLabel.className = "node-sub text-purple-400 font-mono mt-1 w-full text-center";
    txIdLabel.innerText = "CID: AWAITING";
    
    // Reset tracker
    steps.forEach((id) => {
        let el = document.getElementById(id);
        el.classList.remove('active', 'passed', 'failed');
    });

    // Clear logs & animations
    elLog.innerHTML = `<div class="text-center text-[#484f58] italic text-[11px] mt-10" id="empty-state">// System Idle<br>// Waiting for instruction</div>`;
    elLayer.innerHTML = '';
}

function updateTracker(idx, isFailed = false) {
    currentStepIdx = idx;
    steps.forEach((id, i) => {
        const el = document.getElementById(id);
        el.classList.remove('active', 'passed', 'failed');
        if (isFailed) {
            el.classList.add('failed');
            // Highlight previous steps as rolled back logically, or just keep them 'failed' for visual impact
            if (i < idx) el.classList.add('failed'); 
        } else {
            if (i < idx) el.classList.add('passed');
            else if (i === idx) el.classList.add('active');
        }
    });

    // Update Orch string
    let labelArr = ['INITIAL', 'AWAITING_OTP', 'AWAIT_PAYMENT', 'AWAIT_BILLING', 'AWAIT_CARD', 'COMPLETED'];
    if (isFailed) {
        stLabel.innerText = `STATE: COMPENSATING`;
        stLabel.className = "node-sub text-rose-400 font-bold font-mono mt-1 w-full text-center tracking-widest";
    } else {
        stLabel.innerText = `STATE: ${labelArr[idx]}`;
        if(idx === 5) stLabel.className = "node-sub text-emerald-400 font-bold font-mono mt-1 w-full text-center tracking-widest";
    }
}

function appendLog(type, title, desc, cssClass) {
    const empty = document.getElementById('empty-state');
    if (empty) empty.remove();

    const d = document.createElement('div');
    d.className = `log-line ${cssClass}`;
    d.innerHTML = `
        <div class="log-meta"><span>${type}</span><span>${new Date().toISOString().substring(11,23)}</span></div>
        <div class="log-title">${title}</div>
        <div class="log-desc">${desc}</div>
    `;
    elLog.appendChild(d);
    
    // Auto scroll
    requestAnimationFrame(() => {
        elLog.scrollTop = elLog.scrollHeight;
    });
}

// Calculate true center of an element relative to the animation layer
function getCenterCoords(el) {
    if (!el) return { x: 0, y: 0 };
    const rect = el.getBoundingClientRect();
    const layerRect = elLayer.getBoundingClientRect();
    return {
        x: (rect.left + rect.width / 2) - layerRect.left,
        y: (rect.top + rect.height / 2) - layerRect.top
    };
}

// -------------------------------------------------------------
// CORE ANIMATION ROUTER: Sender -> Queue -> Receiver
// -------------------------------------------------------------
function fireMessage(senderId, queueId, receiverId, label, msgClass, callback) {
    const senderNode = document.getElementById(senderId);
    const qNode      = document.getElementById(queueId);
    const recvNode   = document.getElementById(receiverId);

    if (!senderNode || !recvNode) {
        console.error("Animation targets missing", senderId, receiverId);
        if(callback) callback();
        return;
    }

    // Determine visual style
    let icon = msgClass.includes('evt') ? '📢' : 
               msgClass.includes('suc') ? '✅' : 
               msgClass.includes('err') ? '🛑' : '⚡'; // default cmd
    
    let isErrorStr = msgClass.includes('err') ? "pulse-err" : "pulse-tx";

    // Create Packet
    const pkt = document.createElement('div');
    pkt.className = `packet ${msgClass}`;
    pkt.innerHTML = `${icon} <span class="ml-1">${label}</span>`;
    
    // Set timing variables derived from speed 
    // Base times: transit: 1000ms, queue delay: 400ms
    const transitTime = 1200 / speedMultiplier;
    const holdTime = 400 / speedMultiplier;
    
    elLayer.appendChild(pkt);
    
    // Initial state at Sender
    const posStart = getCenterCoords(senderNode);
    pkt.style.transform = `translate(${posStart.x}px, ${posStart.y}px) scale(0)`;
    pkt.style.opacity = '0';
    pkt.style.transition = `transform ${transitTime}ms cubic-bezier(0.34, 1.56, 0.64, 1), opacity ${transitTime}ms, scale ${transitTime}ms`;

    // Pulse Origin
    senderNode.classList.add(isErrorStr);

    // Frame 1: Move to Queue
    requestAnimationFrame(() => {
        const posQ = getCenterCoords(qNode);
        
        pkt.style.transform = `translate(${posQ.x}px, ${posQ.y}px) scale(0.9)`;
        pkt.style.opacity = '1';

        setTimeout(() => {
            senderNode.classList.remove('pulse-tx', 'pulse-err');
            
            // Queue Ingestion state
            qNode.classList.add('q-active-rx');
            pkt.style.opacity = '0.3';
            pkt.style.transform = `translate(${posQ.x}px, ${posQ.y}px) scale(0.5)`;
            
            setTimeout(() => {
                // Queue Emission state
                qNode.classList.remove('q-active-rx');
                qNode.classList.add('q-active-tx');
                
                pkt.style.opacity = '1';
                
                const posEnd = getCenterCoords(recvNode);
                pkt.style.transform = `translate(${posEnd.x}px, ${posEnd.y}px) scale(1.1)`;

                let destPulseClass = msgClass.includes('err') ? "pulse-err" : "pulse-rx";
                recvNode.classList.add(destPulseClass);

                setTimeout(() => {
                    qNode.classList.remove('q-active-tx');
                    // Packet Absorb at Destination
                    pkt.style.transform += ' scale(1.5)';
                    pkt.style.opacity = '0';
                    
                    setTimeout(() => {
                        pkt.remove();
                        recvNode.classList.remove(destPulseClass);
                        if (callback) callback();
                    }, 200 / speedMultiplier);

                }, transitTime);

            }, holdTime);

        }, transitTime);
    });
}

// -------------------------------------------------------------
// SAGA LOGIC & FLOW CONTROL
// -------------------------------------------------------------

function startSaga(mode = null) {
    if(isRunning) return;
    resetSim();
    
    isRunning = true;
    failMode = mode; // 'billing' or 'card'

    // Button states
    document.getElementById('btn-start').disabled = true;
    document.getElementById('btn-fail-bill').disabled = true;
    document.getElementById('btn-fail-card').disabled = true;

    // Start CID
    const trxId = "CID-" + Math.random().toString(10).substring(2,8);
    txIdLabel.innerText = "CID: " + trxId;

    // Turn on Orchestrator
    document.getElementById('srv-payment').querySelector('.orch-ring').classList.add('orch-anim');

    updateTracker(0); // Initial
    appendLog("SYS", "Transaction Genesis", `Allocated memory for Saga Orchestrator. Correlation: ${trxId}`, "log-inf");

    // Begin sequence
    setTimeout(() => {
        appendLog("CMD", "IStartPaymentOrchestration", "Client requesting payment sequence start.", "log-cmd");
        
        fireMessage('srv-client', 'q-orch', 'srv-payment', 'IStartPaymentOrchestration', 'pkt-cmd', () => {
            
            updateTracker(1); // Awaiting OTP
            appendLog("EVT", "IPaymentOtpGenerated", "Fanout to notify boundary.", "log-evt");
            
            fireMessage('srv-payment', 'q-noti', 'srv-notification', 'IPaymentOtpGenerated', 'pkt-evt', () => {
                appendLog("SUC", "Notification Svc", "Sent SMS token to client terminal.", "log-suc");
                
                // Expose OTP action
                document.getElementById('btn-otp').classList.remove('hidden');
            });
        });
    }, 800 / speedMultiplier);
}

function provideOtp() {
    document.getElementById('btn-otp').classList.add('hidden');
    appendLog("USER", "OTP Supplied", "Validating 2FA credentials", "log-inf");

    fireMessage('srv-client', 'q-orch', 'srv-payment', 'IOtpVerified', 'pkt-suc', () => {
        
        updateTracker(2); // Awaiting Payment
        appendLog("CMD", "IPaymentProcessRequested", "Orchestrator requesting internal payment lock.", "log-cmd");

        // Internal path: pmt -> pmt queue -> pmt
        fireMessage('srv-payment', 'q-proc', 'srv-payment', 'IPaymentProcessRequested', 'pkt-cmd', () => {
            
            appendLog("SYS", "Payment Worker", "Local DB state marked 'Processed'.", "log-inf");
            
            fireMessage('srv-payment', 'q-orch', 'srv-payment', 'IPaymentProcessSucceeded', 'pkt-suc', () => {
                triggerBilling();
            });
        });

    });
}

function triggerBilling() {
    updateTracker(3); // Awaiting Bill
    appendLog("CMD", "IBillUpdateRequested", "Orchestrator firing command to Billing context.", "log-cmd");

    fireMessage('srv-payment', 'q-bill', 'srv-billing', 'IBillUpdateRequested', 'pkt-cmd', () => {
        
        if (failMode === 'billing') {
            appendLog("ERR", "Billing Failure", "System rejected bill update.", "log-err");
            
            fireMessage('srv-billing', 'q-bill', 'srv-payment', 'IBillUpdateFailed', 'pkt-err', () => {
                executeRollback('billing');
            });
        } else {
            appendLog("SUC", "Billing Svc", "Bill marked strictly as PAID in DB.", "log-suc");
            
            fireMessage('srv-billing', 'q-bill', 'srv-payment', 'IBillUpdateSucceeded', 'pkt-suc', () => {
                triggerCard();
            });
        }
    });
}

function triggerCard() {
    updateTracker(4); // Awaiting Card
    appendLog("CMD", "ICardDeductionRequested", "Invoking financial deduction bridge.", "log-cmd");

    fireMessage('srv-payment', 'q-card', 'srv-card', 'ICardDeductionRequested', 'pkt-cmd', () => {
        
        if (failMode === 'card') {
            appendLog("ERR", "Card Gateway Denied", "Insufficient limits reported by Network.", "log-err");
            
            fireMessage('srv-card', 'q-card', 'srv-payment', 'ICardDeductionFailed', 'pkt-err', () => {
                executeRollback('card'); // Rolls back card, then bill, then payment
            });
        } else {
            appendLog("SUC", "Card Svc", "Money deducted appropriately.", "log-suc");
            
            fireMessage('srv-card', 'q-card', 'srv-payment', 'ICardDeductionSucceeded', 'pkt-suc', () => {
                finishHappyPath();
            });
        }
    });
}

function finishHappyPath() {
    updateTracker(5); // Completed
    appendLog("EVT", "IPaymentCompleted", "Final domain state confirmed. Broadcasting success.", "log-evt");

    fireMessage('srv-payment', 'q-domain', 'srv-notification', 'IPaymentCompleted', 'pkt-suc', () => {
        appendLog("SYS", "Saga End", "All tasks executed correctly.", "log-inf");
        document.getElementById('srv-payment').querySelector('.orch-ring').classList.remove('orch-anim');
        isRunning = false;
    });
}

// -------------------------------------------------------------
// COMPENSATING TRANSACTIONS (ROLLBACK REVERSES THE FLOW)
// -------------------------------------------------------------

function executeRollback(source) {
    updateTracker(currentStepIdx, true); // Sets UI red
    appendLog("ERR", "Saga Rollback Triggered", `Initiating compensation due to ${source} anomaly.`, "log-err");

    if (source === 'card') {
        // Need to undo Billing
        appendLog("CMD", "IRevertBillUpdateRequested", "Orchestrator attempting to undo Bill status.", "log-cmd"); // Rollbacks act as commands

        fireMessage('srv-payment', 'q-bill', 'srv-billing', 'IRevertBillUpdateRequested', 'pkt-cmd', () => {
            appendLog("SUC", "Billing Compensated", "Bill reset to Unpaid state.", "log-suc");

            fireMessage('srv-billing', 'q-bill', 'srv-payment', 'IRevertBillUpdateSucceeded', 'pkt-evt', () => {
                revertPaymentInternal(); // Continue rollback up the chain
            });
        });
    } else {
        // If billing failed, card was never called, so just undo internal payment
        revertPaymentInternal();
    }
}

function revertPaymentInternal() {
    appendLog("CMD", "IRevertPaymentRequested", "Cancelling payment local intent.", "log-cmd");

    fireMessage('srv-payment', 'q-proc', 'srv-payment', 'IRevertPaymentRequested', 'pkt-cmd', () => {
        appendLog("SUC", "Payment Worker", "Local intent scrubbed to 'Reversed'.", "log-suc");

        fireMessage('srv-payment', 'q-orch', 'srv-payment', 'IPaymentReversed', 'pkt-suc', () => {
            stLabel.innerText = "STATE: FAILED"; 
            
            appendLog("EVT", "IPaymentFailed", "Domain-wide broadcast: Saga Terminated.", "log-evt");
            fireMessage('srv-payment', 'q-domain', 'srv-notification', 'IPaymentFailed', 'pkt-err', () => {
                appendLog("SYS", "Saga Halted", "Clean rollback finalized.", "log-inf");
                document.getElementById('srv-payment').querySelector('.orch-ring').classList.remove('orch-anim');
                isRunning = false;
            });
        });
    });
}