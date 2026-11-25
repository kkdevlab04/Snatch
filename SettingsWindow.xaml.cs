using System.IO;
using System.Windows;

namespace Snatch;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    public event Action? Saved;

    public SettingsWindow(Settings settings)
    {
        InitializeComponent();
        _settings = settings;
        _snips = _settings.Snippets.Select(s => new Snippet { Trigger = s.Trigger, Text = s.Text }).ToList();
        RefreshList();
        HideBox.Text = _settings.AutoHideSeconds.ToString();
        DlText.Text = _settings.ResolvedDownloads;
        SoundChk.IsChecked = _settings.PlaySound;
        ClipChk.IsChecked = _settings.CopyToClipboard;
        PosBox.SelectedValue = _settings.ToastPosition;
        if (PosBox.SelectedIndex < 0) PosBox.SelectedValue = "Right";
        YtChk.IsChecked = _settings.YoutubeDownload;
        UpdateYtDirText();
        UpdateSoundText();
        UpdateCapSoundText();
        UpdateShotDirText();
        ExpandChk.IsChecked = _settings.TextExpand;
        RefreshSnips();
        SetSnipMode(null);
    }

    private readonly List<int> _shown = new();   // 목록 행 → _snips 인덱스 (검색으로 걸러져도 원래 위치 기억)
    private List<Snippet> _snips;                 // 편집본. 전체 저장을 눌러야 _settings에 반영됨
    private bool _snipDirty;                      // 저장 안 한 단축어 변경이 있는지
    private bool _suppress;                       // 목록을 다시 채울 때 편집칸을 덮어쓰지 않게
    private int _editing = -1;                    // 지금 편집 중인 _snips 인덱스, -1이면 새 단축어
    private bool _delArmed;                       // 삭제는 두 번 눌러야 실행

    private void MarkSnipDirty()
    {
        _snipDirty = true;
        SavedText.Text = "단축어 변경됨 · 저장을 눌러야 적용돼요";
    }

    private void RefreshSnips()
    {
        _suppress = true;
        try
        {
            SnipList.Items.Clear();
            _shown.Clear();
            var q = SnipSearch.Text.Trim();
            for (int i = 0; i < _snips.Count; i++)
            {
                var s = _snips[i];
                if (q.Length > 0 && !s.Trigger.Contains(q, StringComparison.OrdinalIgnoreCase) && !s.Text.Contains(q, StringComparison.OrdinalIgnoreCase)) continue;
                _shown.Add(i);
                SnipList.Items.Add($"{s.Trigger}  →  {s.Text.Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ")}");
            }
            SnipList.SelectedIndex = _shown.IndexOf(_editing);   // 편집 중인 항목이 보이면 그대로 선택 유지
        }
        finally { _suppress = false; }
    }

    private void OnSnipSearch(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SnipHint.Visibility = string.IsNullOrEmpty(SnipSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        RefreshSnips();
    }

    // 목록 항목을 누르면 그 단축어를 편집 모드로 불러옴. 저장하면 그 항목이 바뀜(이름 바꿔도 같은 항목).
    private void OnSnipSelect(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        int row = SnipList.SelectedIndex;
        if (_suppress || row < 0 || row >= _shown.Count) return;
        _editing = _shown[row];
        var s = _snips[_editing];
        SnipTrigger.Text = s.Trigger;
        SnipText.Text = s.Text;
        SetSnipMode(s.Trigger);
    }

    private void OnSnipNew(object sender, RoutedEventArgs e)
    {
        _editing = -1;
        SnipList.SelectedIndex = -1;
        SnipTrigger.Clear(); SnipText.Clear();
        SetSnipMode(null);
    }

    // name이 null이면 새 단축어 모드
    private void SetSnipMode(string? name)
    {
        _delArmed = false;
        SnipDelBtn.Content = "삭제";
        SnipDelBtn.IsEnabled = name != null;
        SnipMode.Text = name == null ? "새 단축어" : $"편집 중: {name}";
        SnipSaveBtn.Content = name == null ? "추가" : "수정 저장";
    }

    private void OnSnipSave(object sender, RoutedEventArgs e)
    {
        var t = SnipTrigger.Text.Trim().ToLowerInvariant();
        if (t.Length == 0 || !t.All(ch => char.IsAsciiLetterOrDigit(ch) || (ch >= 'ㄱ' && ch <= 'ㅎ')))   // 영문·숫자·자음(ㄱ~ㅎ)
        {
            MessageBox.Show(this, "단축어는 영문·숫자·자음만 쓸 수 있어요.", "단축어", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (SnipText.Text.Length == 0) return;
        int dup = _snips.FindIndex(s => s.Trigger == t);
        if (_editing >= 0 && _editing < _snips.Count)
        {
            if (dup >= 0 && dup != _editing)
            {
                MessageBox.Show(this, $"'{t}' 단축어는 이미 있어요. 다른 이름을 쓰세요.", "단축어", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _snips[_editing].Trigger = t;
            _snips[_editing].Text = SnipText.Text;
        }
        else if (dup >= 0) _snips[dup].Text = SnipText.Text;   // 같은 단축어면 덮어쓰기
        else _snips.Add(new Snippet { Trigger = t, Text = SnipText.Text });
        MarkSnipDirty();
        RefreshSnips();
        OnSnipNew(sender, e);
    }

    private void OnSnipRemove(object sender, RoutedEventArgs e)
    {
        if (_editing < 0 || _editing >= _snips.Count) return;
        if (!_delArmed) { _delArmed = true; SnipDelBtn.Content = "정말 삭제?"; return; }   // 한 번 더 눌러야 삭제
        _snips.RemoveAt(_editing);
        MarkSnipDirty();
        RefreshSnips();
        OnSnipNew(sender, e);
    }

    private void UpdateShotDirText() => ShotDirText.Text = Capture.ScreenshotFolder(_settings);

    private void UpdateYtDirText() => YtDirText.Text = _settings.ResolvedYoutubeDir;

    private void OnPickYtDir(object sender, RoutedEventArgs e)
    {
        var picked = InputBox.AskFolder(this, "유튜브 다운로드 폴더", _settings.ResolvedYoutubeDir);
        if (picked != null) { _settings.YoutubeDir = picked; UpdateYtDirText(); }
    }

    private void OnOpenYtDir(object sender, RoutedEventArgs e)
    {
        var dir = _settings.ResolvedYoutubeDir;
        try { System.IO.Directory.CreateDirectory(dir); System.Diagnostics.Process.Start("explorer.exe", dir); } catch { }
    }

    private void UpdateCapSoundText() =>
        CapSoundText.Text = string.IsNullOrWhiteSpace(_settings.CaptureSoundFile)
            ? "기본 셔터음" : System.IO.Path.GetFileName(_settings.CaptureSoundFile);

    private void OnPickCapSound(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = "캡처 음원 선택 (.wav)", Filter = "WAV 오디오 (*.wav)|*.wav" };
        if (dlg.ShowDialog(this) == true) { _settings.CaptureSoundFile = dlg.FileName; UpdateCapSoundText(); }
    }

    private void OnDefaultCapSound(object sender, RoutedEventArgs e)
    {
        _settings.CaptureSoundFile = null;
        UpdateCapSoundText();
    }

    private void OnTestCapSound(object sender, RoutedEventArgs e)
        => Sound.PlayCapture(new Settings { PlaySound = true, CaptureSoundFile = _settings.CaptureSoundFile });

    private void OnPickShotDir(object sender, RoutedEventArgs e)
    {
        var picked = InputBox.AskFolder(this, "캡처 저장 폴더", Capture.ScreenshotFolder(_settings));
        if (picked != null)
        {
            _settings.ScreenshotFolder = picked;
            UpdateShotDirText();
        }
    }

    private void UpdateSoundText() =>
        SoundText.Text = string.IsNullOrWhiteSpace(_settings.SoundFile)
            ? "기본 알림음" : System.IO.Path.GetFileName(_settings.SoundFile);

    private void OnPickSound(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.OpenFileDialog
        {
            Title = "알림음 선택 (.wav)",
            Filter = "WAV 오디오 (*.wav)|*.wav",
        };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _settings.SoundFile = dlg.FileName;
            UpdateSoundText();
        }
    }

    private void OnDefaultSound(object sender, RoutedEventArgs e)
    {
        _settings.SoundFile = null;
        UpdateSoundText();
    }

    private void OnTestSound(object sender, RoutedEventArgs e)
    {
        var probe = new Settings { PlaySound = true, SoundFile = _settings.SoundFile };
        Sound.Play(probe);
    }

    private void RefreshList()
    {
        FavList.Items.Clear();
        foreach (var f in _settings.Favorites)
            FavList.Items.Add($"{f.Name}  —  {f.Path}");
    }

    private void OnAddFav(object sender, RoutedEventArgs e)
    {
        var path = InputBox.AskFolder(this, "즐겨찾기 폴더 추가", _settings.ResolvedDownloads);
        if (path != null)
        {
            var name = new DirectoryInfo(path).Name;
            _settings.Favorites.Add(new Favorite { Name = name, Path = path });
            RefreshList();
        }
    }

    private void OnRemoveFav(object sender, RoutedEventArgs e)
    {
        int i = FavList.SelectedIndex;
        if (i >= 0 && i < _settings.Favorites.Count)
        {
            _settings.Favorites.RemoveAt(i);
            RefreshList();
        }
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(HideBox.Text, out var s)) _settings.AutoHideSeconds = Math.Clamp(s, 4, 120);
        _settings.PlaySound = SoundChk.IsChecked == true;
        _settings.CopyToClipboard = ClipChk.IsChecked == true;
        _settings.ToastPosition = PosBox.SelectedValue as string ?? "Right";
        _settings.YoutubeDownload = YtChk.IsChecked == true;
        _settings.TextExpand = ExpandChk.IsChecked == true;
        _settings.Snippets = _snips;   // 단축어는 여기서만 반영
        _settings.Save();
        _snipDirty = false;
        Saved?.Invoke();
        MetaFlash();
    }

    private void MetaFlash()
    {
        SavedText.Text = "저장됨 ✓";
    }

    // 저장 안 한 단축어가 있으면 닫기 전에 확인
    private void OnCloseBtn(object sender, RoutedEventArgs e)
    {
        if (_snipDirty && MessageBox.Show(this, "저장 안 한 단축어 변경이 있어요. 그래도 닫을까요?", "단축어",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        Close();
    }
}
