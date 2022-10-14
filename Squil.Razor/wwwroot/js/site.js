// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

Blazor.registerCustomEventType('hiddenbsmodal', { browserEventName: "hiddenbsmodal" });

function translateEvent(element, oldName, newName) {
    //console.info(`init translating event from '${oldName}' to '${newName}'`);
    $(element).on(oldName, function () {
        //element.addEventListener(oldName, function () {
        const newEvent = new CustomEvent(newName, { bubbles: true });
        //console.info("translating event");
        element.dispatchEvent(newEvent);
    })
}

function callBoostrapModal(element, show) {
    const modal = bootstrap.Modal.getOrCreateInstance(element);

    if (show) {
        modal.show();
    } else {
        modal.hide();
    }
}

function getInnerText(element) {
    const result = element?.innerText ?? "";

    return result;
}

function setInnerText(element, text) {
    if (element && element.innerText != text) {
        //console.info(`set text from '${element.innerText}' to '${text}'`);
        element.innerText = text;
    }
}

function initBootstrapContent() {
    $('[data-toggle="tooltip"]').each(function () {
        const content = $('.tooltip-content', this)[0]

        if (content) {
            console.info(content.innerHTML);
            this.title = content.innerHTML;
            $(this).tooltip({ html: true });
        }
        else {
            $(this).tooltip();
        }
    })
}

function initTooltip(element) {
    const content = element.querySelector(".content");
    const template = element.querySelector(".template");

    $(element).tooltip({ html: true, title: content.innerHTML, template: template.innerHTML, delay: { show: 500, hide: 0 } });
}

function showEphemeralTooltip(element) {
    const content = element.querySelector(".content");
    const template = element.querySelector(".template");

    $(element).tooltip({ html: true, trigger: "manual", title: content.innerHTML, template: template.innerHTML });

    $(element).tooltip("show");

    setTimeout(() => {
        $(element).tooltip("hide");
    }, 1000);
}

function copyInnerTextToClipboard(element) {
    const innerText = element.innerText;
    navigator.clipboard.writeText(innerText);
}