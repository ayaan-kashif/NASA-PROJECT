// WebGL link fix. com.unity.dt.app-ui (pulled in by the Unity AI assistant package) ships libAppUITextMateLib.a,
// a code-highlighting library built against an older Emscripten that still imports _emval_take_value and
// _emval_as. Newer Emscripten renamed them, so the link fails. T-MINUS never calls that library at runtime;
// these stubs only satisfy the linker and warn if they are ever reached.
mergeInto(LibraryManager.library, {
  _emval_take_value: function (type, arg) {
    console.warn("T-MINUS: _emval_take_value stub called (App UI TextMate is not used in the game).");
    return 0;
  },
  _emval_as: function (handle, returnType, destructorsRef) {
    console.warn("T-MINUS: _emval_as stub called (App UI TextMate is not used in the game).");
    return 0;
  }
});
