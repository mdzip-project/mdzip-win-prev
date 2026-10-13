Status: awaiting-test
Last: Branch fix/block-scripts: preview shown in a sandboxed iframe so document scripts never run (spec §13.2); verified end to end with the real renderer and panel; not committed

Spec test T-016 (hostile-content.mdz, canary server): main fails the MUST cases script-element, event-handler and svg-inline-script; this branch passes every MUST case. The SHOULD cases for http: loads (image, stylesheet, style, iframe, object) still request the canary until the WebView2 move (#2).

<!-- Dashboard reads these two lines. -->
