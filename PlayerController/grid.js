var scale, gridX, gridY, offset, player
var playerVector, playerDestination;
var offsetVector, offsetDestination;
var offsetX = 5;
var offsetY = 5;
var allPaths = new Array();
var layers = new Array();
var maxLayer = 3;
var currentLayer = 0;

for (var i = -maxLayer; i <= maxLayer; i++) {
    var layer = new Layer();
    layers.push(project.activeLayer);
}

function activateLayer(index) {
    layers[getLayerIndex(index)].activate();
    layers[getLayerIndex(index)].opacity = 1;
    if (currentLayer != index) {
        layers[getLayerIndex(currentLayer)].opacity = 0.2;
        currentLayer = index;
    }
}

function getLayerIndex(index) {
    if (index < -maxLayer || index > maxLayer) {
        alert("Layer out of bounds. Current layer range: -" + maxLayer + " to " + maxLayer);
    }
    // layers array represents z-indexes from -maxLayer to maxLayer
    return index + maxLayer;
}

activateLayer(currentLayer);

gridApi.setScale = function (newScale) {
    scale = newScale;
    gridX = new Point(scale, 0);
    gridY = new Point(0, scale);
    offset = new Point(offsetX * scale, offsetY * scale);
}

function onMouseDrag(event) {
    updateOffset(event.delta);
}

function updateOffset(delta) {
    offset += delta;
    for (var i = 0; i < allPaths.length; i++) {
        allPaths[i].position += delta;
    }
    if (playerDestination) {
        playerDestination += delta;
    }
}

function onFrame(event) {
    if (playerVector) {
        var distance = player.position - playerDestination;
        if (distance.length > playerVector.length) {
            player.position += playerVector;
        }
        else {
            player.position = playerDestination;
            playerVector = null;
            playerDestination = null;

            playerPositionAbsolute = player.position - offset;
            offsetDestinationX = ($("#gridPanel").width() / 2) - playerPositionAbsolute.x;
            offsetDestinationY = ($("#gridPanel").height() / 2) - playerPositionAbsolute.y;

            offsetDestination = new Point(offsetDestinationX, offsetDestinationY);
            offsetVector = (offsetDestination-offset) / 10;
        }
    }
    if (offsetVector) {
        var distance = offset - offsetDestination;
        if (distance.length > offsetVector.length) {
            updateOffset(offsetVector);
        }
        else {
            updateOffset(offsetDestination-offset);
            offsetVector = null;
            offsetDestination = null;
        }
    }
}

gridApi.drawGrid = function (minX, minY, maxX, maxY) {
    function gridLine(start, end) {
        var path = new Path();
        path.strokeColor = "#D0D0D0";
        path.add(start, end);
        allPaths.push(path);
    }

    // draw the vertical lines
    for (var x = minX; x <= maxX; x++) {
        var start = gridPoint(x, minY)
        var end = gridPoint(x, maxY);
        gridLine(start, end);
    };

    // draw the horizontal lines
    for (var y = minY; y <= maxY; y++) {
        var start = gridPoint(minX, y)
        var end = gridPoint(maxX, y);
        gridLine(start, end);
    };
}

function gridPoint(x, y) {
	return (gridX * x) + (gridY * y) + offset;
}

var firstBox = true;

gridApi.drawBox = function (x, y, z, width, height, border, borderWidth, fill, sides) {
    activateLayer(z);
    // if this is the very first room, centre the canvas by updating the offset
    if (firstBox) {
        var centrePoint = gridPoint(x + width / 2, y + height / 2);
        var offsetX = ($("#gridPanel").width() / 2) - centrePoint.x;
        var offsetY = ($("#gridPanel").height() / 2) - centrePoint.y;
        updateOffset(new Point(offsetX, offsetY));
        firstBox = false;
    }
    var path = new Path();
    path.strokeColor = border;
    path.strokeWidth = borderWidth;
    var points = [gridPoint(x, y), gridPoint(x + width, y), gridPoint(x + width, y + height), gridPoint(x, y + height)];
    // sides is encoded with bits to represent NESW
    var draw = [sides & 8, sides & 4, sides & 2, sides & 1];
    var drewLast = false;
    var drewAny = false;
    for (var i = 0; i < 4; i++) {
        var next = (i + 1) % 4;
        if (draw[i]) {
            if (!drewLast) path.add(points[i]);
            path.add(points[next]);
            drewLast = true;
            drewAny = true;
        }
        else {
            if (drewAny) {
                allPaths.push(path);
                path = new Path();
                path.strokeColor = border;
                path.strokeWidth = borderWidth;
                drewAny = false;
            }
            drewLast = false;
        }
    }
    if (drewAny) {
        allPaths.push(path);
    }
    var fillPath;
    if (sides == 15) {
        fillPath = path
    }
    else {
        fillPath = new Path();
        fillPath.add(points[0], points[1], points[2], points[3]);
        allPaths.push(fillPath);
    }
    fillPath.fillColor = fill;
    fillPath.closed = true;
}

gridApi.drawLine = function (x1, y1, x2, y2, border, borderWidth) {
    var path = new Path;
    path.strokeColor = border;
    path.strokeWidth = borderWidth;
    path.add(gridPoint(x1, y1));
    path.add(gridPoint(x2, y2));
    allPaths.push(path);
}

gridApi.drawPlayer = function (x, y, z, radius, border, borderWidth, fill) {
    activateLayer(z);
    if (!player) {
        player = new Path.Circle(gridPoint(x, y), radius);
        player.strokeColor = border;
        player.strokeWidth = borderWidth;
        player.fillColor = fill;
        player.fillColor = fill;
        allPaths.push(player);
    }
    else {
        playerDestination = gridPoint(x, y);
        playerVector = (playerDestination - player.position) / 10;
        // move player to the end of the activeLayer so it gets drawn on top
        project.activeLayer.addChild(player);
    }
    player.opactity = 0.5;
}

gridApi.drawLabel = function (x, y, z, text) {
    activateLayer(z);
    var pointText = new PointText(gridPoint(x, y));
    pointText.justification = "center";
    pointText.fillColor = "black";
    pointText.content = text;
    allPaths.push(pointText);
}

gridApi.onLoad();