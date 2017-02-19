[目次](Index.md)

---

# 設定ファイルリファレンス

ここでは、設定ファイルの詳細について説明します。

* [設定ファイルについて](#設定ファイルについて)
    * [設定ファイルの格納場所](#設定ファイルの格納場所)
    * [設定ファイルの形式](#設定ファイルの形式)
* [オブジェクトリファレンス](#オブジェクトリファレンス)
    * [ルートオブジェクト](#ルートオブジェクト)
    * [ActualProxyオブジェクト](#ActualProxyオブジェクト)
    * [Credentialオブジェクト](#Credentialオブジェクト)
    * [GUIオブジェクト](#GUIオブジェクト)
    * [Listenerオブジェクト](#Listenerオブジェクト)
    * [MainWindowオブジェクト](#MainWindowオブジェクト)
    * [Placementオブジェクト](#Placementオブジェクト)
    * [Pointオブジェクト](#Pointオブジェクト)
    * [Proxyオブジェクト](#Proxyオブジェクト)
    * [Rectオブジェクト](#Rectオブジェクト)
    * [SystemSettingsSwitcherオブジェクト](#SystemSettingsSwitcherオブジェクト)


## 設定ファイルについて

### 設定ファイルの格納場所

コマンド版もGUI版も同じ設定ファイルを利用します。
設定ファイルは以下のパスにあります。

```
%LOCALAPPDATA%\MAPE\Settings.json
```

通常、`%LOCALAPPDATA%` は以下のフォルダになります。

```
C:\Users\<ユーザー名>\AppData\Local
```


### 設定ファイルの形式

設定ファイルはテキストファイル（符号化はUTF-8）です。
内容は、[ルートオブジェクト](#ルートオブジェクト)をJson形式で記述したものになります。


## オブジェクトリファレンス

### ルートオブジェクト

「認証プロキシ爆発しろ！」設定全体を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| Credential | [Credentialオブジェクト](#Credentialオブジェクト)の配列 | [] | 認証プロキシに対する認証情報。 |
| Culture | 文字列 | 下の注記を参照してください | UIのカルチャ。 |
| GUI | [GUIオブジェクト](#GUIオブジェクト) | {} | GUIに対する設定。 Windows版のみで有効です。 |
| LogLevel | 文字列 | "Error" | 出力するログのレベル。下の[LogLevelの値](#LogLevelの値)を参照してください。 |
| Proxy | [Proxyオブジェクト](#Proxyオブジェクト) | {} | 中継機能に対する設定。 |
| SystemSettingsSwitcher | [SystemSettingsSwitcherオブジェクト](#SystemSettingsSwitcherオブジェクト) | {} | プロキシ設定書き換え機能に対する設定。 |

#### Cultureの省略値

Cultureが設定されていない場合は、
スレッドの現在のカルチャ/UIカルチャがそのまま使用されます。
スレッドの現在のカルチャ/UIカルチャは通常システムの既定値です。

#### LogLevelの値

| 値 | 意味 |
|:----|:----|
| "Error" | Errorレベルのログを出力します。 |
| "Info" | Info, Warning, Errorレベルのログを出力します。 |
| "Off" | ログを出力しません。 |
| "Verbose" | すべてのログを出力します。 |
| "Warning" | Warning, Errorレベルのログを出力します。 |


### ActualProxyオブジェクト

本来のプロキシ（認証プロキシ）の情報を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| Host | 文字列 | 必須 | プロキシのホスト名。 |
| Port | 整数 | 必須 | プロキシのポート。 |


### Credentialオブジェクト

認証プロキシに対する認証情報を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| EnableAssumptionMode | boolean | true | 対象となる認証プロキシに[Basic認証決めつけモード](AdvancedUsage.md)を適用するかどうか。 |
| EndPoint | 文字列 | 必須 | 対象となる認証プロキシのエンドポイント。例 "proxy.example.com:8080" |
| Persistence | 文字列 | "Persistent" | 認証情報の保存方法。下の[Persistenceの値](#Persistenceの値)を参照してください。 |
| ProtectedPassword | 文字列 | "" | 暗号化されたパスワード。 |
| UserName | 文字列 | "" | ユーザー名 |

#### Persistenceの値

| 値 | 意味 |
|:----|:----|
| "Persistent" | 認証情報は暗号化して設定ファイルに保存されます。 |
| "Process" | 認証情報はツールが起動している間のみ保持されます。 |
| "Session" | 認証情報はhttpセッションの間のみ保持されます。 |

#### 注意事項

* `ProtectedPassword`プロパティの暗号化のキーはユーザーごとに異なります。
つまり、この値を別ユーザーや別PCにコピーすると復号できなくなります。
この値は、WindowsのData Protection APIを用いて暗号化しています。


### GUIオブジェクト

GUIの設定を表すオブジェクトです。
Windows版のみで有効です。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| ChaseLastLog | boolean | true | ログ一覧にログが追加された際に、最後のログを自動的に追尾するかどうか。 |
| MainWindow | [MainWindowオブジェクト](#MainWindowオブジェクト) | {} | メインウィンドウのレイアウト設定。 |


### Listenerオブジェクト

接続を待ち受けるリスナーの設定を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| Address | 文字列 | "127.0.0.1" | 待ち受けるIPアドレス。 |
| Backlog | 整数 | 8 | 接続受付キューの長さ（socketに指定するbacklog）。正整数でなければなりません。 |
| Port | 整数 | 8888 | 待ち受けるポート。 |


### MainWindowオブジェクト

メインウィンドウのレイアウト設定を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| LogListViewColumnWidths | 浮動小数点数の配列 | なし | ログ表示リストビューの各カラムの幅です。 |
| Placement | [Placementオブジェクト](#Placementオブジェクト) | {} | メインウィンドウのウィンドウ位置設定。 |


### Placementオブジェクト

ウィンドウの位置設定を表すオブジェクトです。
Win32 APIの`WINDOWPLACEMENT`構造体に対応しています。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| Flags | 整数 | ０ | `WINDOWPLACEMENT`構造体の`flags`メンバに対応。 |
| MaxPosition | [Pointオブジェクト](#Pointオブジェクト) | {"X": 0, "Y": 0} | `WINDOWPLACEMENT`構造体の`ptMaxPosition`メンバに対応。 |
| MinPosition | [Pointオブジェクト](#Pointオブジェクト) | {"X": 0, "Y": 0} | `WINDOWPLACEMENT`構造体の`ptMinPosition`メンバに対応。 |
| NormalPosition | [Rectオブジェクト](#Rectオブジェクト) | {"Left": 0, "Top": 0, "Right": 0, "Bottom": 0} | `WINDOWPLACEMENT`構造体の`rcNormalPosition`メンバに対応。 |
| ShowCmd | 整数 | ０ | `WINDOWPLACEMENT`構造体の`showCmd`メンバに対応。 |


### Pointオブジェクト

座標平面上の点を表すオブジェクトです。
Win32 APIの`POINT`構造体に対応しています。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| X | 整数 | 0 | `POINT`構造体の`x`メンバに対応。 |
| Y | 整数 | 0 | `POINT`構造体の`y`メンバに対応。 |


### Proxyオブジェクト

中継機能の設定を表すオブジェクトです。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| AdditionalListeners | [Listenerオブジェクト](#Listenerオブジェクト)の配列 | [] | 追加のリスナー。 |
| MainListener | [Listenerオブジェクト](#Listenerオブジェクト) | {} | メインとなるリスナー。 |
| RetryCount | 整数 | 2 | 通信がエラーになった場合にリトライを試みる回数。 |

#### 注意事項

* プロキシ設定書き換え機能が有効な場合、`MainListener`が書き換え後のプロキシとなります。
* `MainListener`が指定されていない場合、[Listenerオブジェクト](#Listenerオブジェクト)のデフォルト値が適用され、
`127.0.0.1:8888`がメインリスナーの待ち受けエンドポイントになります。
* `RetryCount`は、通信エラーの場合のリトライもありますが、
実際には認証失敗した場合のリトライ回数とみなしてよいでしょう。
ツールと認証プロキシの間でユーザーに見えないやり取りがあったりするので、
「だいたいこのくらいの回数リトライする」と思ってください。


### Rectオブジェクト

座標平面上の矩形を表すオブジェクトです。
Win32 APIの`RECT`構造体に対応しています。

| 名前 | 型 | 省略時の値 | 内容 |
|:----|:----|:----|:----|
| Bottom | 整数 | 0 | `RECT`構造体の`bottom`メンバに対応。 |
| Left | 整数 | 0 | `RECT`構造体の`left`メンバに対応。 |
| Right | 整数 | 0 | `RECT`構造体の`right`メンバに対応。 |
| Top | 整数 | 0 | `RECT`構造体の`top`メンバに対応。 |


### SystemSettingsSwitcherオブジェクト

プロキシ設定書き換え機能の設定を表すオブジェクトです。

| 名前 | 型 | デフォルト値 | 内容 |
|:----|:----|:----|:----|
| ActualProxy | [ActualProxyオブジェクト](#ActualProxyオブジェクト) | 下の注を参照 | 認証プロキシの情報。 |
| EnableSystemSettingsSwitch | boolean | true | プロキシ設定書き換えを行うかどうか。 |
| ProxyOverride | 文字列 | "" | 【Windowsのみ】プロキシ設定書き換え後の「プロキシを利用しないアドレス」のリスト。例 "*.example.com;*.example.local;&lt;local&gt;" |

#### 注意事項

* `ActualProxy`プロパティが存在しない場合、
ツールは現在の設定から認証プロキシの情報を取得します。
* プロキシ設定書き換え後に「ローカルアドレスにはプロキシサーバーを使用しない」設定にするには、
`ProxyOverride`プロパティ値のアドレスリストに`<local>`を追加します。
