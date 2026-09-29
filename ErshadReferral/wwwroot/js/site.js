// ===== Modern Site Interactions =====

document.addEventListener('DOMContentLoaded', function () {

    // Auto-dismiss alerts after 5 seconds
    var alerts = document.querySelectorAll('.alert-professional');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            var bsAlert = bootstrap.Alert.getOrCreateInstance(alert);
            if (bsAlert) bsAlert.close();
        }, 5000);
    });

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(function (anchor) {
        anchor.addEventListener('click', function (e) {
            var target = document.querySelector(this.getAttribute('href'));
            if (target) {
                e.preventDefault();
                target.scrollIntoView({ behavior: 'smooth', block: 'center' });
            }
        });
    });

    // Button click feedback - subtle scale animation
    document.querySelectorAll('.office-action-btn, .btn-exit-visitor').forEach(function (btn) {
        btn.addEventListener('mousedown', function () {
            this.style.transform = 'scale(0.97)';
        });
        btn.addEventListener('mouseup', function () {
            this.style.transform = '';
        });
        btn.addEventListener('mouseleave', function () {
            this.style.transform = '';
        });
    });

    // Auto-hide validation errors after correction
    document.querySelectorAll('.form-control, .form-select').forEach(function (input) {
        input.addEventListener('input', function () {
            var validationSpan = this.parentElement.querySelector('.text-danger');
            if (validationSpan && this.value.trim()) {
                validationSpan.textContent = '';
            }
        });
    });

    // Confirm before leaving form with unsaved data
    var formDirty = false;
    var forms = document.querySelectorAll('form[method="post"]');
    forms.forEach(function (form) {
        form.addEventListener('submit', function () {
            formDirty = false;
        });
        form.querySelectorAll('input, textarea, select').forEach(function (el) {
            el.addEventListener('change', function () {
                formDirty = true;
            });
        });
    });

    window.addEventListener('beforeunload', function (e) {
        if (formDirty) {
            e.preventDefault();
            e.returnValue = '';
        }
    });
});
