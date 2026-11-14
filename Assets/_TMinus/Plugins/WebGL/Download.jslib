// Lets the game hand the browser a file to save (the debrief's "Save picture" button). WebGL cannot write to the disk itself.
mergeInto(LibraryManager.library, {
  TMinusDownload: function (namePtr, dataPtr, size) {
    var name = UTF8ToString(namePtr);
    var bytes = new Uint8Array(HEAPU8.buffer, dataPtr, size).slice();   // copy: the game's memory can move
    var blob = new Blob([bytes], { type: "image/png" });
    var a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = name;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    setTimeout(function () { URL.revokeObjectURL(a.href); }, 5000);
  }
});
