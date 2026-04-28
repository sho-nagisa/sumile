// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function fitShiftTables() {
    document.querySelectorAll(".shift-table-fit").forEach(wrapper => {
        const table = wrapper.querySelector("table");
        if (!table) return;

        table.style.transform = "";
        wrapper.style.height = "";
        wrapper.style.removeProperty("--shift-table-scale");
    });
}

window.addEventListener("load", fitShiftTables);
window.addEventListener("resize", fitShiftTables);
