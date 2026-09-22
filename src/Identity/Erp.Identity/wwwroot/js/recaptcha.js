// Only ever loaded when reCAPTCHA is actually required for the current attempt — see
// SignIn.razor/SignUp.razor. grecaptcha itself comes from the Google script tag rendered
// alongside this one (?render=<site key>), which defines window.grecaptcha.
window.erpRecaptcha = {
    execute: function (siteKey, action) {
        return new Promise(function (resolve, reject) {
            grecaptcha.ready(function () {
                grecaptcha.execute(siteKey, { action: action }).then(resolve, reject);
            });
        });
    },

    // SignIn.razor posts through a plain <form>, not a Blazor-bound submit, so there's no C#
    // handler to await execute() from — this intercepts the native submit once, fills the hidden
    // g-recaptcha-response field, then re-submits for real.
    guardSubmit: function (formId, fieldId, siteKey, action) {
        var form = document.getElementById(formId);
        var field = document.getElementById(fieldId);

        form.addEventListener('submit', function (event) {
            if (field.value) return;

            event.preventDefault();
            erpRecaptcha.execute(siteKey, action).then(function (token) {
                field.value = token;
                form.submit();
            });
        });
    }
};
