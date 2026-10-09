namespace AndroidWireless
{
    /// <summary>Android 모듈이 없는 에디터에서 보이는 화면. 안내만 하고 기기 탐색·페어링은 할 수 없다.</summary>
    internal sealed class ModuleMissingView : ViewBase
    {
        public ModuleMissingView() : base("ModuleMissing") { }
    }
}
