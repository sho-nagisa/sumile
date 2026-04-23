// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function fitShiftTables() {
    document.querySelectorAll(".shift-table-fit").forEach(wrapper => {
        const table = wrapper.querySelector("table");
        if (!table) return;

        table.style.transform = "";
        wrapper.style.height = "";
        wrapper.style.setProperty("--shift-table-scale", "1");

        if (!window.matchMedia("(max-width: 767.98px)").matches) return;

        const tableWidth = table.scrollWidth;
        const wrapperWidth = wrapper.clientWidth;
        if (!tableWidth || !wrapperWidth || tableWidth <= wrapperWidth) return;

        const scale = wrapperWidth / tableWidth;
        wrapper.style.setProperty("--shift-table-scale", scale.toString());
        wrapper.style.height = `${table.offsetHeight * scale}px`;
    });
}

window.addEventListener("load", fitShiftTables);
window.addEventListener("resize", fitShiftTables);
