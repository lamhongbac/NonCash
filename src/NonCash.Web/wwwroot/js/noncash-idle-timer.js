// Idle timeout timer for NonCash Blazor web app.
// Calls the DotNet helper when the user has been inactive for the configured duration.

(function () {
    let dotNetHelper = null;
    let timeoutMs = 30 * 60 * 1000; // default 30 minutes
    let timerId = null;
    const events = ['mousemove', 'mousedown', 'keydown', 'touchstart', 'scroll', 'click'];

    function resetTimer() {
        if (timerId) {
            clearTimeout(timerId);
        }
        if (dotNetHelper && timeoutMs > 0) {
            timerId = setTimeout(() => {
                dotNetHelper.invokeMethodAsync('OnIdleTimeout')
                    .catch(err => console.error('Idle timeout invocation failed:', err));
            }, timeoutMs);
        }
    }

    function onActivity() {
        resetTimer();
    }

    window.noncashIdleTimer = {
        start: function (helper, idleTimeoutMs) {
            if (dotNetHelper) {
                // Already started; just update timeout and reset.
                timeoutMs = idleTimeoutMs;
                resetTimer();
                return;
            }

            dotNetHelper = helper;
            timeoutMs = idleTimeoutMs;

            events.forEach(function (event) {
                document.addEventListener(event, onActivity, { passive: true });
            });

            resetTimer();
        },

        stop: function () {
            if (timerId) {
                clearTimeout(timerId);
                timerId = null;
            }

            events.forEach(function (event) {
                document.removeEventListener(event, onActivity);
            });

            dotNetHelper = null;
        },

        reset: function () {
            resetTimer();
        }
    };
})();
