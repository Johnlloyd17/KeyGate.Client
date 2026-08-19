window.chartInstances = {};

window.createChart = function (canvasId, config) {
    var canvas = document.getElementById(canvasId);
    if (!canvas) return;

    if (window.chartInstances[canvasId]) {
        window.chartInstances[canvasId].destroy();
    }

    var ctx = canvas.getContext('2d');
    window.chartInstances[canvasId] = new Chart(ctx, config);
};

window.destroyChart = function (canvasId) {
    if (window.chartInstances[canvasId]) {
        window.chartInstances[canvasId].destroy();
        delete window.chartInstances[canvasId];
    }
};

window.updateChart = function (canvasId, config) {
    if (window.chartInstances[canvasId]) {
        window.chartInstances[canvasId].destroy();
    }
    var canvas = document.getElementById(canvasId);
    if (!canvas) return;
    var ctx = canvas.getContext('2d');
    window.chartInstances[canvasId] = new Chart(ctx, config);
};
