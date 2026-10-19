# Phase 1B.2A: read-only isolation and resource lifecycle preflight

Date: 2026-10-19. Repository: `0ecf21ae4722e8c2867b2cdb2a2ab0eedde911cb`. Inspection snapshots 13:56-14:02 UTC / 17:26-17:32 Tehran; values are not persistent capacity guarantees. Only read-only host probes and documentation edits were performed. No features, services, models, distributions or VMs were changed; no candidate code was compiled or executed. Local identifiers, absolute user paths and raw inventories are omitted.

## Observed facts

| Area / evidence | Observation |
|---|---|
| Windows CIM / WSL version | Windows 11 Pro, build 26200; WSL reports revision 26200.9457 |
| CPU CIM | i7-12700H, 14 cores / 20 logical processors; HypervisorPresent=true |
| CPU counter | Three bounded one-second total-processor samples at 14:02 UTC: 8.6%, 0.0%, 4.3%; mean 4.3%. This is a transient load sample, not peak capacity or a startup benchmark |
| Optional features, Win32_OptionalFeature | VirtualMachinePlatform and Microsoft-Windows-Subsystem-Linux: InstallState=1 (enabled). Full Hyper-V, its hypervisor/services/management features, HypervisorPlatform and Containers-DisposableClientVM: InstallState=2 (disabled) |
| Feature-query limitation | Get-WindowsOptionalFeature -Online required elevation and was not retried with elevation. Hyper-V PowerShell commands were not found; virtualization/v2 inventory returned invalid namespace; vmms service was not found |
| CPU prerequisite flags | CIM reported SLAT, VM monitor extensions and firmware virtualization flags false while a hypervisor is active. This is not reliable proof that the physical CPU lacks these prerequisites; their independent firmware exposure remains unverified |
| RAM | 15.67 GiB visible physical memory; approximately 1.14-1.20 GiB available across adjacent samples. Commit approximately 28.72 GiB / 38.67 GiB limit. Available RAM, not installed RAM or pagefile capacity, controls admission |
| Storage | C: 31.38 GiB free / 305.10 GiB; D: 67.61 / 500.27; E: 128.25 / 146.49. All report healthy NTFS; physical-disk inventory reports one approximately 953.87 GiB NVMe SSD. Partition free space is not independent physical-disk fault isolation |
| WSL | Version 2.5.9.0, kernel 6.6.87.2-1, default version 2. One registered docker-desktop distribution, stopped. No distro was launched |
| Existing WSL configuration | User settings: memory=2304MB, processors=2, swap=4GB; autoMemoryReclaim=gradual. These are stored settings, not a measured effective allocation of the stopped VM |
| Docker | Client 28.3.0. Docker service stopped/manual; no Docker backend process observed. Desktop settings file existed but selected legacy field names did not yield values. Effective engine mode, Desktop version and Resource Saver setting are unverified; no daemon query/start was attempted |
| Shared services | vmcompute and hns running/manual; WslService running/automatic. Presence does not establish full Hyper-V availability |
| Ollama | Local GET /api/version=0.40.5; GET /api/ps returned zero resident models. Two Ollama application/server processes had about 40 MiB combined working sets; this is not model memory |
| GPU | NVIDIA RTX 3060 Laptop, driver 610.88; 6144 MiB total, 436 MiB used, 5559 MiB free, 0% utilization. This snapshot is whole-device accounting, not process ownership or peak usage |
| Development activity | Visual Studio was open. It and other user workloads were left alone. Current low RAM cannot be attributed solely to any one application from these probes |

Read probes: CIM, Get-Counter, Get-Service/Get-Process/Get-Command, volume/disk inventory, WSL status/list/version, Docker client version, nvidia-smi and Ollama GET endpoints. Settings reads selected resource fields only. [Feature-state definitions](https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-optionalfeature) explain CIM values. An active hypervisor can obscure prerequisite reporting; [Microsoft requirements](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/host-hardware-requirements) do not establish local feature state.

**Current admission decision: defer allocation.** Approximately 1.2 GiB available RAM fails the proposed headroom checks below. Do not free memory by stopping user applications, shutting down WSL or unloading their models. No feature is assumed available merely from the Pro edition.

## Isolation choice and trade-offs

| Option | Boundary / reproducibility | Resource and lifecycle implications | Decision |
|---|---|---|---|
| Disposable Hyper-V Linux VM | Separate kernel; pinned offline image, SDK and verifier can be hash-bound. Guest permission/process separation still protects tests from the candidate | Proposed 2 GiB active allocation, configurable up to 4 GiB after admission; zero guest RAM/CPU when off. Additional hypervisor/VMM overhead must be measured. Cold boot/teardown unmeasured; allow up to 90s startup as an initial deadline, not an observed latency | Preferred conditionally. Full role is currently disabled; feature approval and coexistence validation required |
| Windows Sandbox | Hypervisor boundary and disposable session. SDK must be supplied offline each session; host-derived OS makes exact image pinning less direct. Defaults include integrations that must be disabled/verified | MemoryInMB is configurable; Windows plus SDK may need more memory than a minimal Linux guest. Startup/shutdown and resource-release costs unmeasured. Not presently enabled | Alternative if explicit Windows semantics are needed; less direct automated worker/evidence control |
| Docker on existing WSL2 | Containers share the WSL Linux kernel and development utility VM. Docker socket, host mounts and integration are serious escape/cross-workload surfaces | Existing 2304MB WSL cap is shared, not a private worker reservation. Easy reuse but teardown of containers does not prove utility-VM RAM release; never issue global wsl --shutdown for lab cleanup | Not the primary adversarial-code boundary |
| Dedicated WSL distro | More separation than sharing a container filesystem, but still shares the WSL utility VM/kernel and host integration | Could terminate only an owned distro, but must not terminate Docker/user distros or reconfigure global WSL limits | Not equivalent to the proposed dedicated VM boundary |
| Full Windows VM / another hypervisor | Full VM useful later for Windows-specific cases; adds licensing/image cost. A second hypervisor adds unverified compatibility and tooling | Usually a larger active budget; cold images/toolchains not measured here. Hardware acceleration for alternative tools is unverified | Defer; no installation or configuration proposed now |

Linux verification establishes Linux/.NET behavior only. Use no guest GPU. A sealed, patched offline image must contain hash-pinned SDK/runtime/references and test dependencies; its availability remains unverified. [Sandbox controls](https://learn.microsoft.com/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-configure-using-wsb-file) and [Hyper-V memory controls](https://learn.microsoft.com/en-us/windows-server/virtualization/hyper-v/dynamic-memory) describe capabilities, not local results. Prefer fixed RAM during each pilot job; optional dynamic memory cannot replace shutdown or admission checks.

## Resource-On-Demand policy (proposed, not implemented)

**Normal Development Mode:** no active lab guest/model/server, watchdog, polling, preloading or autostart. Retain bounded evidence and an offline disk image. Shared Windows/WSL/Docker/Ollama infrastructure remains user-owned: its service overhead is distinct from heavy lab allocations. Total kernel/driver overhead was not measured; global memory/VRAM need not be zero.

**Active Lab Mode:** one explicitly initiated session, one heavy phase at a time, effective limits recorded before allocation. All numerical limits and paths below must be configurable and validated; these are pilot estimates, not hard-coded reservations or calibrated performance claims.

| Policy | Proposed starting point / admission rule |
|---|---|
| Guest memory | Request 2 GiB for small trusted compile/test fixtures; permit 1-4 GiB profiles only after calibration. The chosen maximum active allocation, not just startup memory, must fit headroom. No VM exists at idle |
| Host headroom | H=max(configured 4 GiB, configured 25% of visible RAM). Reserve an additional estimated O=1 GiB for VMM/supervisor/transient overhead. Admit only when available A >= guest budget B + O + H; for B=2 GiB this means at least 7 GiB available |
| Commit | Require remaining commit capacity >= planned incremental peak plus a configurable safety margin (initially 2 GiB). Pagefile space does not make physical-RAM admission pass |
| CPU | 1-2 configured vCPUs, one candidate. Defer when a short preflight sample shows sustained host CPU over a configurable 75% threshold. Effective CPU cap semantics must be measured on this hybrid-core CPU |
| GPU | Evaluation allocation zero. For inference, require measured incremental RAM/VRAM estimates plus safety margins (initial VRAM margin 1 GiB); do not size from model file bytes alone. If estimates are unknown, require a separately approved calibration session |
| Disk | Cached base estimated 4-8 GiB; per-job writable growth cap initially 8 GiB, evidence cap 256 MiB. Require free space >= uncached base + worst-case remaining writable growth + evidence + 20 GiB reserve. C: fails this initial uncached provisioning budget; D:/E: are candidates, subject to an approved dedicated directory and quota |
| Candidate and I/O | Existing draft starting limits remain configurable: candidate memory 512 MiB, PID cap 32, source 256 KiB, each output stream 1 MiB, writable workspace 128 MiB. Keep trusted supervisor outside candidate limits/process |
| Deadlines | Initial startup 90s, compilation 60s, invocation 5s, test phase 30s; execution window 240s including startup, then collect/teardown 30s and release checks 30s. Record an absolute VM lease of allocation-start + execution + teardown windows (initially 270s); expiry forces owned-VM power-off even if evidence is incomplete. All configurable; supersedes earlier illustrative whole-job/4GB assumptions |
| Active observation | Phase-boundary samples plus a bounded active-only interval, initially 5s. On physical headroom below 2 GiB or repeated configured pressure violations, stop dispatch, preserve evidence and cancel only the owned workload. Thresholds/intervals/violation counts must be configurable |

Never evict user workloads to meet admission. Recheck immediately before allocation; defer on missing telemetry. Record startup/peak/stop RAM, guest allocation, GPU baseline, CPU and cleanup duration during future calibration. Counters race with other applications, so this is a bounded safety policy, not a guarantee against arbitrary concurrent host pressure. Emergency owned-VM force-off may precede evidence collection; record missing evidence rather than await candidate cooperation.

## Session lifecycle and recovery

`Idle -> Preflight -> Allocate -> Verify Isolation -> Evaluate -> Collect Evidence -> Destroy -> Verify Resource Release -> Idle`

| State | Required action / failure behavior |
|---|---|
| Idle | No active lab resources or timer. User explicitly starts a session; inspect any prior ownership journal before admitting new work |
| Preflight | Validate configuration, privileges, effective features, offline image/toolchain hashes, fresh resource samples and ownership. Journal session ID, VM identity, process creation identity, dedicated paths, budgets and absolute deadlines. If blocked, return a reason without allocation |
| Allocate | Create only session-owned disposable resources. Register partial ownership before each mutation so failed creation can be recovered. No autostart/checkpoint/save-state resume; future VM settings must explicitly disable boot autostart |
| Verify Isolation | Verify no NIC, host shares, clipboard, GPU or credentials; restricted candidate account/process, trusted test ownership, watchdog and disk/resource limits. A failed check goes straight to evidence/teardown, never candidate execution |
| Evaluate | Compile/run only in a later approved isolated verifier. Candidate cannot load into the supervisor, forge verdict files or read hidden tests. Cancellation, deadline or host pressure stops new work; no host fallback |
| Collect Evidence | Bounded collection of supervisor-owned structured results and raw diagnostics; validate/hash as untrusted data. No automatic execution, preview, archive traversal or unbounded export. Preserve partial evidence on failure |
| Destroy | Independent cleanup deadline/token, even after caller cancellation. Graceful shutdown then bounded force-off of the positively identified lab VM only. Remove its registration/differencing/output disks after bounded collection; preserve sealed base and final evidence |
| Verify Resource Release | Confirm owned VM off/unregistered, guest allocation zero, owned workers/model residents absent and transient disks removed. Record post-stop RAM/VRAM/CPU vs baseline as supporting observations, not proof that every global counter returns exactly to baseline. Mark released, uncertain or failed; never label a failed release as clean Idle |

An active-only host watchdog, independent of controller/guest and tied to exact VM identity and lease, handles ordinary controller loss and exits after teardown. If all host watchdog/control processes die, immediate automatic cleanup is **not guaranteed** without OS-managed recovery; quotas still bound the orphan. Disable VM autostart and saved-state resume. After restart, the next explicit lab launch inspects stale journals before allocation. No boot task or recovery daemon is installed here.

Recovery matches VM ID, ownership markers, image/root lineage and process creation identity, not names/PIDs alone. Ambiguous resources remain untouched for review; never recursively delete inferred directories. Cleanup errors retain established correctness observations but block allocation. Startup, guest, evidence and timeout failures share bounded teardown. Forced restart may leave incomplete logs.

## Sequential inference and residency cleanup

Source: [ExecutionProfile](../LocalModelLab.Core/Definitions.cs), [BenchmarkService](../LocalModelLab.Core/Execution.cs), [OllamaProvider](../LocalModelLab.Infrastructure/OllamaProvider.cs). All profiles/defaults use keepAliveSeconds=300, sent explicitly. providerManaged skips residency preparation. singleModel unloads every non-target digest, including unrelated models, then polls up to 20 times at 100ms within the attempt timeout. No ownership check exists. Run finalization writes state without unloading the last model; cancellation proves neither server completion nor release.

[Ollama's FAQ](https://docs.ollama.com/faq#how-do-i-keep-a-model-loaded-in-memory-or-make-it-unload-immediately) describes expiration, zero-duration unloading and request-level overrides. [GET /api/ps](https://docs.ollama.com/api/ps) reports residency. No expiry/unload experiment was performed; current zero residency is a snapshot, not cleanup validation.

Proposed future change, separately approved and versioned:

1. Require an exclusively lab-owned endpoint/process for destructive residency control, or leave the shared endpoint untouched and defer the lab. A digest absent at session start does not prove exclusive ownership later: /api/ps exposes no per-client lease. Do not use existing singleModel on a shared development server.
2. Within that dedicated session, retain finite keep-alive for benchmark comparability. A new profile may choose zero for low-residency behavior, but that changes loading costs between attempts and must not silently alter the completed benchmark/profile.
3. On completion/failure/cancellation, issue bounded unload requests only for the session's owned models, using a cleanup token independent of cancelled inference. Verify disappearance from /api/ps; record cleanup requests, outcomes and release observations separately from inference timing/results. If unresolved, terminate only a positively owned lab server/runner process tree under the approved recovery policy. Never stop the user's Ollama service or unrelated models.
4. Sequence: infer -> freeze outputs -> release owned residency -> verify host headroom -> allocate evaluation guest -> collect -> destroy -> verify release. Alternatively evaluate already-frozen artifacts in an evaluation-only session with no inference server launch. Do not keep the guest idling during inference. Shared-user activity that consumes the headroom causes defer, not eviction.

[Docker Resource Saver](https://docs.docker.com/desktop/use-desktop/resource-saver/) pauses the engine on WSL rather than stopping its shared utility VM; it does not itself release that VM's RAM. Stored gradual autoMemoryReclaim is not measured release. Leave user [WSL configuration](https://learn.microsoft.com/en-us/windows/wsl/wsl-config) and lifecycle alone.

## Unknowns, approvals and next milestone

Security risks remain even with a VM: guest/hypervisor vulnerabilities, compromised guest-side verdicts, hostile evidence/file parsers and mistaken resource ownership. Pin and patch the image, disable unnecessary integration channels, isolate candidate permissions, authenticate/bound the chosen evidence protocol and verify exact required checks. Do not mount an attacker-controlled guest filesystem on the host as a convenient export shortcut. Image hashes and no-network configuration do not prove runtime integrity; containment and tamper tests are separate gates.

Unknown/blocked: elevated DISM feature confirmation and firmware prerequisites; real Hyper-V/Docker/WSL coexistence after enablement; effective Docker settings/daemon state; guest image/license/SDK/test cache; startup/peak/cleanup measurements; effective CPU caps; safe offline transfer and anti-tamper demonstration. No guest or candidate execution was used to close these gaps. Current RAM pressure is an admission blocker, not permission to close user applications.

Required approvals before live provisioning: optional feature enablement/reboot if chosen; isolated image/download provenance and storage root/quota; privileged, VM-scoped lifecycle access; active budgets and force-off/orphan policy; dedicated inference endpoint ownership; later trusted containment smoke tests. Generated-code execution needs its own gate after containment is demonstrated. No global service/WSL cleanup or persistent monitor is authorized.

Next proposed milestone: **1B.2B configuration, admission planning and ownership journal with mocked lifecycle tests**, no real VM or Ollama mutations. Preserve explicit application-service orchestration. Acceptance: reject missing capabilities and today's low-RAM snapshot; validate all limits; allocate nothing on failed preflight; never mutate shared workloads; test cancellation, timeout, partial allocation, forged/stale ownership, controller-loss and restart recovery; zero idle polling; record teardown/release failure without rewriting correctness. Then separately approve **1B.2C trusted VM lifecycle/containment calibration** with no generated code. Measure resource return and coexistence before implementing an executable-code evaluator.

Read-only preflight is complete; provisioning readiness is not established. Documentation edits require review; no commit/push was made.
