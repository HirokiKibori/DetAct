var editor;

function loadCodeEditor(dontNetObjRef) {
    editor = CodeMirror.fromTextArea(document.getElementById('btml_code'), {
        mode: "markdown",
        lineWrapping: true,
        lineNumbers: true,
        matchBrackets: true
    });

    $('.CodeMirror').resizable({
        resize: function () {
            editor.setSize($(this).width(), $(this).height());
        }
    });

    editor.on("change", editor => {
        dontNetObjRef.invokeMethodAsync("UpdateCode", editor.getValue());
    });
}

function setContentCodeEditor(content) {
    if (!editor) return;

    editor.setValue(content);
    setTimeout(function () {
        editor.refresh();
    }, 1);
}