using System.Globalization;

namespace LeviShare.App;

/// <summary>UI strings. Bound as <c>L[Key]</c>; a new instance is created when the language changes.</summary>
public sealed class Loc(bool japanese)
{
    public bool Japanese { get; } = japanese;

    public static bool SystemIsJapanese => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ja";

    public string this[string key] => (Japanese ? Ja : En).TryGetValue(key, out var v) ? v : En.GetValueOrDefault(key, key);

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    static readonly Dictionary<string, string> En = new()
    {
        ["Theme"] = "Appearance",
        ["ThemeAuto"] = "System",
        ["ThemeLight"] = "Light",
        ["ThemeDark"] = "Dark",
        ["ClearSearch"] = "Clear search",
        ["SelectedOnly"] = "Selected only",
        ["Search"] = "Search by name, version or folder…",
        ["SelectionCount"] = "{0} / {1} selected",
        ["ChooseInstance"] = "Choose an instance",
        ["EmptyHint"] = "Choose your LeviLauncher data folder and an instance above to see its mods and packs.",
        ["NoMatches"] = "No matching items. Clear the search or turn off Selected only.",
        ["NothingToShare"] = "This instance has no mods or packs to share.",
        ["Ready"] = "Select at least one mod or pack.",
        ["Complete"] = "Your zip is ready to share.",
        ["ExportingShort"] = "Creating your zip…",
        ["ImportHint"] = "Send the zip and these steps. The recipient restores it in LeviLauncher.",
        ["ViewSteps"] = "View import steps",
        ["DataFolder"] = "LeviLauncher data",
        ["Change"] = "Change...",
        ["Reload"] = "Reload",
        ["Instance"] = "Instance",
        ["NoData"] = "LeviLauncher's data folder was not found. Use \"Change...\" to pick the folder that contains \"versions\".",
        ["NoInstances"] = "No instances found in this folder.",
        ["UwpInstance"] = "UWP instances cannot load mods and are not supported.",
        ["InstanceNotFound"] = "The instance could not be read.",
        ["SharedData"] = "Packs are read from the game data shared with the game itself (version isolation is off).",
        ["LipTitle"] = "Mods installed with lip",
        ["LipNote"] = "Only the name and version are shared. LeviLauncher installs them again with lip when restoring.",
        ["RawTitle"] = "Mods added by hand",
        ["RawNote"] = "Shared as files. Logs, debug symbols (.pdb) and per-PC IDs are left out.",
        ["ResourceTitle"] = "Resource packs",
        ["BehaviorTitle"] = "Behavior packs",
        ["SkinTitle"] = "Skin packs",
        ["Disabled"] = "disabled",
        ["Dependency"] = "dependency",
        ["Encrypted"] = "Marketplace packs are never shared: {0}",
        ["None"] = "(none)",
        ["SelectAll"] = "All",
        ["SelectNone"] = "None",
        ["Summary"] = "{0} lip mods · {1} mods · {2} packs",
        ["SummarySize"] = "{0:N0} files · {1} before compression",
        ["Counting"] = "Counting files…",
        ["Excluded"] = "{0} files left out (logs, .pdb, IDs)",
        ["Repaired"] = "{0} pack manifests cleaned up for LeviLauncher (content unchanged)",
        ["Export"] = "Export share zip...",
        ["Exporting"] = "Exporting... large setups can take a minute.",
        ["SaveTitle"] = "Save share zip",
        ["Exported"] = "Saved: {0}",
        ["OpenFolder"] = "Open folder",
        ["CopySteps"] = "Copy steps for the recipient",
        ["Copied"] = "Copied to the clipboard.",
        ["NothingSelected"] = "Select at least one mod or pack.",
        ["Error"] = "Error: {0}",
        ["ImportSteps"] =
            "1. In LeviLauncher, open Settings > Others and turn on \"Instance backup (experimental)\".\n" +
            "2. Download Minecraft {0} and create an instance.\n" +
            "3. Open that instance's settings > Manage > \"Restore backup\" and choose the zip.\n" +
            "4. Mods installed with lip are installed again automatically (lip must be installed: Settings > Third-party components).\n" +
            "5. Resource packs are copied but not switched on: turn them on in Minecraft (Settings > Global resources).",
        ["About"] = "LeviShare {0} · MIT License · not affiliated with LiteLDev or Mojang",
    };

    static readonly Dictionary<string, string> Ja = new()
    {
        ["Theme"] = "外観",
        ["ThemeAuto"] = "システム",
        ["ThemeLight"] = "ライト",
        ["ThemeDark"] = "ダーク",
        ["ClearSearch"] = "検索をクリア",
        ["SelectedOnly"] = "選択済みのみ",
        ["Search"] = "名前・バージョン・フォルダを検索…",
        ["SelectionCount"] = "{0} / {1} 件を選択",
        ["ChooseInstance"] = "インスタンスを選択",
        ["EmptyHint"] = "上部で LeviLauncher のデータフォルダとインスタンスを選ぶと、Mod とパックが表示されます。",
        ["NoMatches"] = "該当する項目がありません。検索をクリアするか「選択済みのみ」を解除してください。",
        ["NothingToShare"] = "このインスタンスには共有できる Mod・パックがありません。",
        ["Ready"] = "Mod かパックを 1 つ以上選んでください。",
        ["Complete"] = "共有用 zip の準備ができました。",
        ["ExportingShort"] = "共有用 zip を作成中…",
        ["ImportHint"] = "zip と一緒に手順も送れます。受け取る人は LeviLauncher から復元できます。",
        ["ViewSteps"] = "取り込み手順を見る",
        ["DataFolder"] = "LeviLauncher のデータ",
        ["Change"] = "変更...",
        ["Reload"] = "再読み込み",
        ["Instance"] = "インスタンス",
        ["NoData"] = "LeviLauncher のデータフォルダが見つかりません。「変更...」から versions フォルダを含むフォルダを選んでください。",
        ["NoInstances"] = "このフォルダにはインスタンスがありません。",
        ["UwpInstance"] = "UWP 版のインスタンスは Mod を読み込めないため対応していません。",
        ["InstanceNotFound"] = "インスタンスを読み込めませんでした。",
        ["SharedData"] = "バージョン分離がオフのため、ゲーム本体と共有のデータからパックを読んでいます。",
        ["LipTitle"] = "lip で入れた Mod",
        ["LipNote"] = "名前とバージョンだけを共有します。復元時に LeviLauncher が lip で入れ直します。",
        ["RawTitle"] = "手動で入れた Mod",
        ["RawNote"] = "ファイルごと共有します。ログ・デバッグ情報（.pdb）・PC 固有の ID は除きます。",
        ["ResourceTitle"] = "リソースパック",
        ["BehaviorTitle"] = "ビヘイビアパック",
        ["SkinTitle"] = "スキンパック",
        ["Disabled"] = "無効",
        ["Dependency"] = "依存",
        ["Encrypted"] = "マーケットプレイスのパックは共有できません: {0}",
        ["None"] = "（なし）",
        ["SelectAll"] = "すべて",
        ["SelectNone"] = "なし",
        ["Summary"] = "lip Mod {0} 件・Mod {1} 件・パック {2} 件",
        ["SummarySize"] = "{0:N0} ファイル・圧縮前 {1}",
        ["Counting"] = "ファイルを数えています…",
        ["Excluded"] = "{0} 個のファイルを除外（ログ・.pdb・ID）",
        ["Repaired"] = "{0} 個のパックの manifest を LeviLauncher 用に整形（内容は同じ）",
        ["Export"] = "共有用 zip を書き出す...",
        ["Exporting"] = "書き出し中…（大きい構成では 1 分ほどかかります）",
        ["SaveTitle"] = "共有用 zip の保存先",
        ["Exported"] = "保存しました: {0}",
        ["OpenFolder"] = "フォルダを開く",
        ["CopySteps"] = "受け取る人への手順をコピー",
        ["Copied"] = "クリップボードにコピーしました。",
        ["NothingSelected"] = "Mod かパックを 1 つ以上選んでください。",
        ["Error"] = "エラー: {0}",
        ["ImportSteps"] =
            "1. LeviLauncher の 設定 > その他 で「インスタンスバックアップ（実験的）」をオンにする。\n" +
            "2. Minecraft {0} をダウンロードしてインスタンスを作る。\n" +
            "3. そのインスタンスの設定 > 管理 >「バックアップを復元」で zip を選ぶ。\n" +
            "4. lip で入れる Mod は自動で入ります（lip が必要: 設定 > サードパーティ製コンポーネント）。\n" +
            "5. リソースパックは入りますが有効にはならないので、Minecraft の 設定 > グローバルリソース で有効にする。",
        ["About"] = "LeviShare {0} · MIT ライセンス · LiteLDev・Mojang とは無関係の非公式ツール",
    };
}
