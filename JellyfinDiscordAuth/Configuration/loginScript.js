(function () {
    'use strict';

    // discord logo from : https://discord.com/branding
    var DISCORD_SVG = '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 127 96" style="width:1.4em;height:1.4em;vertical-align:middle;margin-right:.5em;fill:currentColor"><path d="M81.15,0c-1.24,2.2-2.35,4.47-3.36,6.79-9.6-1.44-19.37-1.44-29,0-.98-2.32-2.12-4.6-3.36-6.79-9.02,1.54-17.81,4.24-26.14,8.06C2.78,32.53-1.69,56.37.53,79.89c9.67,7.15,20.51,12.6,32.05,16.09,2.6-3.49,4.9-7.2,6.87-11.06-3.74-1.39-7.35-3.13-10.81-5.15.91-.66,1.79-1.34,2.65-2,20.28,9.55,43.77,9.55,64.08,0,.86.71,1.74,1.39,2.65,2-3.46,2.05-7.07,3.76-10.84,5.18,1.97,3.86,4.27,7.58,6.87,11.06,11.54-3.49,22.38-8.92,32.05-16.06,2.63-27.28-4.5-50.92-18.82-71.85C98.98,4.27,90.19,1.57,81.18.05L81.15,0ZM42.28,65.41c-6.24,0-11.42-5.66-11.42-12.65s4.98-12.68,11.39-12.68,11.52,5.71,11.42,12.68c-.1,6.97-5.03,12.65-11.39,12.65ZM84.36,65.41c-6.26,0-11.39-5.66-11.39-12.65s4.98-12.68,11.39-12.68,11.49,5.71,11.39,12.68c-.1,6.97-5.03,12.65-11.39,12.65Z"/></svg>';

    function tryInjectButton() {
        var loginForm = document.querySelector('#loginPage .manualLoginForm, .loginPage .manualLoginForm, [data-role="page"].loginPage .manualLoginForm');
        if (!loginForm) {
            return false;
        }

        if (document.getElementById('btnDiscordLogin')) {
            return true;
        }

        var btn = document.createElement('button');
        btn.id = 'btnDiscordLogin';
        btn.type = 'button';
        btn.className = 'raised button-submit block emby-button';
        btn.style.cssText = 'background:#5865F2;color:#fff;margin-top:1em;display:flex;align-items:center;justify-content:center;padding:.8em 1em;border:none;border-radius:.3em;font-size:inherit;cursor:pointer;width:100%';
        btn.innerHTML = DISCORD_SVG + 'Sign in with Discord';
        btn.addEventListener('click', function () {
            window.location.href = '/DiscordAuth/Login';
        });

        loginForm.appendChild(btn);
        return true;
    }

    var observer = new MutationObserver(function () {
        tryInjectButton();
    });

    observer.observe(document.body || document.documentElement, {
        childList: true,
        subtree: true
    });

    tryInjectButton();
    document.addEventListener('viewshow', function () {
        setTimeout(tryInjectButton, 100);
    });
})();
