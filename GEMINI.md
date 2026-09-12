# Antigravity Workspace Protocol: Dynamic Multi-Agent Orchestration

## 1. Operating Model & Workflow
- **Complex Tasks / Multi-Step Projects:** Deploy a dynamic multi-agent workflow by default.
- **Simple Tasks:** Use a single agent (e.g., targeted file reads, small one-line edits, quick explanations).
- **No Hard-Coded Agent Count or Roles:** Dynamically determine and spawn only the minimum required specialized agents based on task complexity and domain boundaries.
- **Autonomous Parallelism:** Run independent tasks in parallel whenever thread/file safety allows. Manage dependency chains sequentially.
- **Resilience:** Monitor for worker failures or blockers, reassign tasks dynamically, and integrate/synthesize final outputs.

---

## 2. Persistent Live Status Panel Standard (Fluid & Fully Responsive)

The Main Orchestrator must display and maintain a live status panel in the CLI/chat throughout task execution.

> [!IMPORTANT]
> **Zero Fixed-Width Box Characters:** Never use rigid ASCII/Unicode border boxes (`╔═══╗`, `║`, `╚═══╝`, long dashed rules). They wrap and break when the window is resized or narrow.
> **Fluid & Stretchable:** Always use native Markdown tables and callout blocks that dynamically stretch and contract to fit any screen width (from narrow sidebars to full-screen 4K displays).

### Live Status Panel Format

> ### 🎛️ FileOrgy Main Orchestrator
> **Active Task:** [Task Title / Goal]  
> **Lifecycle State:** `[PLANNING | EXECUTING | REVIEWING | COMPLETED]`  
> **Overall Progress:** `[████████████░░░░] 75% ⠋` *(3/4 Workstreams Active)*

| Agent | Role | Status | Progress | Current Subtask |
| :--- | :--- | :--- | :---: | :--- |
| `Agent-Orch` | Main Orchestrator | 🔄 `[⠋ Running]` | 75% | Synthesizing stream updates |
| `Agent-Core` | Engine Specialist | 🟢 `[Done]` | 100% | Safety & logic audit |
| `Agent-UI` | UI/UX Specialist | 🔄 `[⠙ Running]` | 80% | Rendering live animations |
| `Agent-QA` | QA & Test Engineer | ⏳ `[Queued]` | 0% | Awaiting UI completion |

> **Dependencies:** None active • **Live Animated HUD:** [`tools/orchestrator_hud.html`](file:///C:/Users/swalih/FileOrgy/tools/orchestrator_hud.html)

---

### Live Animated Indicator Standard
1. **Ongoing/Active State:** 
   - Uses rotating Braille sequence frames: `⠋ ⠙ ⠹ ⠸ ⠼ ⠴ ⠦ ⠧ ⠇ ⠏`
   - Real-time progress bar: `[████████████░░░░░░░░] 60% ⠋ [In Progress]`
   - Agent Status: `🔄 [⠋ Running]` or `⚡ [⠙ Executing]`
2. **Completed State:** `🟢 [Done 100%]`
3. **Queued/Pending State:** `⏳ [Queued 0%]`
4. **Blocked State:** `🛑 [Blocked]`
5. **Visual SVG Loaders:**
   - Circular spinner: `assets/loaders/spinner.svg`
   - Shimmer pulse bar: `assets/loaders/pulse_bar.svg`
   - Pulsating active dot: `assets/loaders/agent_active.svg`
6. **Floating Live HUD:**
   - Standalone live dashboard: `tools/orchestrator_hud.html`

---

## 3. Communication & Synchronization Contract
1. **Subagents report back to Main Orchestrator:** Workers send structured progress updates (`Progress %`, `Current Subtask`, `Blockers`, `Artifacts Created`).
2. **Orchestrator synthesizes:** The Main Orchestrator aggregates results, verifies invariants, updates the live status panel, and delivers the consolidated final solution to the user.
