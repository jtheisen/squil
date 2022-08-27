// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


function openBoostrapModal(element, show) {
    console.info("modal" + show);
    $(element).modal({ show: false });
    $(element).modal(show ? 'show' : 'hide');

//    if (show) {
//        modal.show();
//    } else {
//        modal.hide();
//    }
}


function getInnerText(element) {
    return element?.innerText ?? "";
}
