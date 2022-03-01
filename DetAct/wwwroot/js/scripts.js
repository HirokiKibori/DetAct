function loadDragableScript(dontNetObjRef) {
    //src: http://jsfiddle.net/Eybxe/2/

    $(function () {
        $("#draggable").draggable({
            scroll: true
        });


        $('#draggable').parent().resizable();
    });
}