// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


function openBoostrapModal(element, show) {
    $(element).modal({ show: false });
    $(element).modal(show ? 'show' : 'hide');
}


function getInnerText(element) {
    const result = element?.innerText ?? "";

    console.info(`got text '${result}'`);

    return result;
}

function setInnerText(element, text) {
    if (element && element.innerText != text) {
        console.info(`set text from '${element.innerText}' to '${text}'`);
        element.innerText = text;
        //window.getSelection().selectAllChildren(element);
    }
}
