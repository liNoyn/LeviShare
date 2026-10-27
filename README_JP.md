# LeviShare

[English](README.md)

[LeviLauncher](https://github.com/LiteLDev/LeviLauncher) の構成（Mod とリソースパック・ビヘイビアパック）を、zip 1 つにまとめて共有するツールです。
zip は LeviLauncher 自身のインスタンスバックアップ形式なので、**受け取る人は改造していない普通の LeviLauncher だけで取り込めます**
（LeviLauncher の「バックアップを復元」を使います）。LeviShare が必要なのは送る側だけです。

> 非公式ツールです。LiteLDev・LeviMC・Mojang とは関係ありません。

## ダウンロード

[Releases](../../releases) から `LeviShare-x.y.z-win-x64.zip` を取り、展開して `LeviShare.exe` を起動します。
インストールや .NET の導入は不要です（Windows 10/11 64bit）。

## 構成を共有する

検索は名前・バージョン・フォルダ名が対象です。**すべて／なし** は表示中の項目だけを変え、**選択済みのみ** は表示を絞るだけで書き出す内容は変えません。見た目は [LeviLauncher](https://github.com/LiteLDev/LeviLauncher) の配色に合わせていて、システム／ライト／ダークを切り替えられます。

1. LeviShare を起動します。LeviLauncher のデータ（`%APPDATA%\levilauncher.exe`、または LeviLauncher の設定で変えた場所）を自動で見つけます。
   見つからないときは **変更...** から `versions` を含むフォルダを選んでください。
2. インスタンスを選びます。
3. 共有するものにチェックを入れます。
   - **lip で入れた Mod** … 名前とバージョンだけを記録し、受け取った側の LeviLauncher が入れ直します。
   - **手動で入れた Mod** … ファイルごと入れます。無効にしている Mod は最初からチェックが外れています。
   - **リソースパック・ビヘイビアパック・スキンパック** … ゲームの共有フォルダのものです。スキンパックは最初はチェックなし。
4. **共有用 zip を書き出す...** で保存し、その zip を送ります。
5. **受け取る人への手順をコピー** で、下の取り込み手順をクリップボードにコピーできます。

LeviShare は LeviLauncher とゲームのファイルを読むだけで、保存する zip 以外は何も書き込みません。

## 取り込む（受け取る人・普通の LeviLauncher）

1. LeviLauncher の **設定 > その他** で **インスタンスバックアップ（実験的）** をオンにする。
2. 同じバージョンの Minecraft をダウンロードしてインスタンスを作る。
3. そのインスタンスの設定 > **管理** > **バックアップを復元** で zip を選ぶ。
4. lip で入れる Mod は自動で入ります。lip が必要です（**設定 > サードパーティ製コンポーネント**）。
5. リソースパックは入りますが有効にはならないので、Minecraft の **設定 > グローバルリソース** で有効にする。

## 入れないもの

| 入れないもの | 理由 |
| --- | --- |
| ワールド・スクリーンショット・アカウント別フォルダ（`Users/<ID>`） | 個人のデータで、フォルダ名も PC ごとに違う |
| マーケットプレイスのパック（暗号化） | 再配布できない |
| Mod 内の `logs`・`cache`・`temp`・クラッシュダンプ・`*.pdb`・`*.log` | 不要で大きいことがある |
| 名前に `uuid` を含むファイル（`ll_bstats_telemetry_uuid` など） | PC 固有の ID |

ゲームは読めても厳密な JSON としては読めないパックの manifest（コメント、文字列中の改行、最後の `}` の後ろの文字）は、
同じ内容で書き直して入れます。こうしないと LeviLauncher が復元時にパックを UUID で照合できません。

## 制限

- 有効にしているグローバルリソースパックの一覧は LeviLauncher が復元しないため、受け取った人がゲーム内で有効にします。
- 受け取る人は LeviLauncher の実験的機能（インスタンスバックアップ）をオンにする必要があります。
- 対応は GDK 版のインスタンスのみです。UWP 版は Mod を読み込めません。
- 再配布してよい Mod・パックだけを共有してください。zip 内の `levishare-info.json` に、Mod・パックの一覧と各 Mod DLL の SHA-256 が入っているので、受け取った人が確認できます。

## zip の形式

LeviLauncher のインスタンスバックアップ（`formatVersion` 2）そのものです。`backup-manifest.json`、
`gameData/Users/Shared/games/com.mojang/<パック>`、`mods/raw/<Mod フォルダ>` からなり、lip パッケージは manifest に記録されます。
LeviLauncher の `internal/mcservice/instance_backup.go` を参照。

## ビルド

.NET 8 SDK が必要です。

```
dotnet test tests/LeviShare.Tests
dotnet run --project src/LeviShare.App
powershell -ExecutionPolicy Bypass -File tools/publish.ps1   # artifacts/ にリリース zip
```

## ライセンス

MIT（[LICENSE](LICENSE)）
