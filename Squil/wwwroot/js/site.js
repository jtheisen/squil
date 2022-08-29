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

function openBoostrapModal(element, show) {
    $(element).modal({ show: false });
    $(element).modal(show ? 'show' : 'hide');
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
