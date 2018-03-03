[目次](Index.md)

---

# コマンドラインオプションリファレンス

ここでは、コマンドラインオプションの詳細について説明します。

* [コマンドラインオプションについて](#コマンドラインオプションについて)
* [コマンドラインオプションリファレンス](#コマンドラインオプションリファレンス)
    * [ActualProxy](#ActualProxy)
    * [AdditionalListeners](#AdditionalListeners)
    * [Culture](#Culture)
    * [EnableSystemSettingsSwitch](#EnableSystemSettingsSwitch)
    * [Help](#Help)
    * [MainListener](#MainListener)
    * [NoLogo](#NoLogo)
    * [NoSettings](#NoSettings)
    * [ProxyOverride](#ProxyOverride)
    * [RetryCount](#RetryCount)
    * [Save](#Save)
    * [SettingsFile](#SettingsFile)
    * [Start](#Start)


## コマンドラインオプションについて

コマンドラインオプションは、以下のいずれかの形式をしています。

* /名前
* /名前:値

先頭のスラッシュ（/）はハイフン（-）でもかまいません。

コマンドラインオプションの名前は大文字小文字を区別しません。

特に記載がなければ、
同じ名前のコマンドラインオプションを複数個記述すると、
最後に記述したものが有効になります。

設定ファイルの設定値に該当するコマンドラインオプションが指定された場合、
コマンドラインオプションによる指定が設定ファイルの内容を上書きします。


## コマンドラインオプションリファレンス

### ActualProxy

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: Jsonオブジェクト（[ActualProxyオブジェクト](Reference_Settings.md#Proxyオブジェクト)）
* 対応する設定: [SystemSettingsSwitcherオブジェクト](Reference_Settings.md#SystemSettingsSwitcherオブジェクト)の`ActualProxy`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合は、認証プロキシを自動検出します。

認証プロキシのアドレスを指定します。

通常、認証プロキシのアドレスは自動的に検出するため、
このオプションを指定する必要はありません。
認証プロキシを明に指定したい場合に指定します。

#### 例

認証プロキシとして`proxy.example.com:8080`を明に指定する例です。

Jsonのプロパティ名を引用符で括る必要があるため、
オプションの値全体を引用符で括り、
さらにその中でプロパティ名をエスケープした引用符で括っていることに注意してください。

```
mape.exe /ActualProxy:"{\"Host\": \"proxy.example.com\", \"Port\": 8080}"
```


### AdditionalListeners

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: Jsonオブジェクト（[Listenerオブジェクト](Reference_Settings.md#Listenerオブジェクト)）の配列
* 対応する設定: [Proxyオブジェクト](Reference_Settings.md#Proxyオブジェクト)の`AdditionalListeners`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合は、リスナーを追加しません。

[MainListener](#MainListener)に加えて、
接続を待ち受けるリスナーを追加する場合に指定します。

リスナーとして外部ネットワークに接続されているアドレスを指定しないでください。
外部からオープンプロキシとして利用されてしまう危険性があります。

#### 例

メインリスナーに加えて、`10.0.75.1:8888`と`192.168.137.1:8888`を追加リスナーとして指定する例です。
これらはHyper-Vの内部仮想スイッチに接続されているアドレスだとします。

Jsonのプロパティ名を引用符で括る必要があるため、
オプションの値全体を引用符で括り、
さらにその中でプロパティ名をエスケープした引用符で括っていることに注意してください。

```
mape.exe /AdditionalListeners:"[{\"Address\": \"10.0.75.1\", \"Port\": 8888},{\"Address\": \"192.168.137.1\", \"Port\": 8888}]"
```


### Culture

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 文字列
* 対応する設定: [ルートオブジェクト](Reference_Settings.md#ルートオブジェクト)の`Culture`プロパティ
* 指定しなかった場合: 現在のスレッドのカルチャ/UIカルチャがそのまま利用されます。

UIで用いるカルチャを指定します。

#### 例

UIのカルチャを英語にします。

```
mape.exe /Culture:en-US
```


### EnableSystemSettingsSwitch

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: true/false
* 対応する設定: [SystemSettingsSwitcherオブジェクト](Reference_Settings.md#SystemSettingsSwitcherオブジェクト)の`EnableSystemSettingsSwitch`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合は、値がtrueであるとみなされます。

「認証プロキシ爆発しろ！」が通信の中継を行う間、
ユーザーのプロキシ設定を書き換えるかどうか指定します。
trueを指定すると、
ユーザーのプロキシ設定を書き換え、
「認証プロキシ爆発しろ！」の[MainListener](#MainListener)をプロキシとして登録します。
falseを指定すると、
ユーザーのプロキシ設定を書き換えずに中継機能を稼働させます。

#### 例

ユーザーのプロキシ設定を書き換えずに中継機能を稼働させる場合の例です。

```
mape.exe /EnableSystemSettingsSwitch:false
```


### Help

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 値なし
* 対応する設定: なし
* 指定しなかった場合: コマンド実行種別を変更しません

「認証プロキシ爆発しろ！」の使い方を表示します。
`/?`と表記することもできます。

このコマンドラインオプションは「認証プロキシ爆発しろ！」のコマンド実行種別を`ShowUsage`に変更します。

#### 例

コマンドの使い方を表示させる例です。

```
mape.exe /Help
```


### LogLevel

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: "Error", "Info", "Off", "Verbose", "Warning"
* 対応する設定: [ルートオブジェクト](Reference_Settings.md#ルートオブジェクト)の`LogLevel`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合は、値がErrorであるとみなされます。

出力するログのレベルを指定します。
値の詳細については、[ルートオブジェクト](Reference_Settings.md#ルートオブジェクト)の`LogLevel`プロパティを参照してください。

#### 例

すべてのログを出力させる場合の例です。

```
mape.exe /LogLevel:Verbose
```


### MainListener

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: Jsonオブジェクト（[Listenerオブジェクト](Reference_Settings.md#Listenerオブジェクト)）
* 対応する設定: [Proxyオブジェクト](Reference_Settings.md#Proxyオブジェクト)の`MainListener`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合、メインリスナーのエンドポイントは`127.0.0.1:8888`になります

接続を待ち受けるメインリスナーを明に指定します。

リスナーとして外部ネットワークに接続されているアドレスを指定しないでください。
外部からオープンプロキシとして利用されてしまう危険性があります。

#### 例

メインリスナーとして`127.0.0.1:9000`を指定する例です。

Jsonのプロパティ名を引用符で括る必要があるため、
オプションの値全体を引用符で括り、
さらにその中でプロパティ名をエスケープした引用符で括っていることに注意してください。

```
mape.exe /MainListener:"{\"Address\": \"127.0.0.1\", \"Port\": 9000}"
```


### NoLogo

* サポート対象
    * [x] Windows コマンド
    * [ ] Windows GUI
* 値の型: 値なし
* 対応する設定: なし
* 指定しなかった場合: 通常通りロゴを出力します。

コマンド起動時のロゴ出力を抑止します。


### NoSettings

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 値なし
* 対応する設定: なし
* 指定しなかった場合: 設定ファイルを読み込みます。

設定ファイルを読み込まないようにします。
この場合、必要な設定はすべてコマンドラインから指定する必要があります。

通常は使用しません。
デバッグやテスト目的で用意されています。

#### 例

設定ファイルを読み込まずにコマンドを起動する例です。

```
mape.exe /NoSettings
```

### ProxyOverride

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 文字列
* 対応する設定: [SystemSettingsSwitcherオブジェクト](Reference_Settings.md#SystemSettingsSwitcherオブジェクト)の`ProxyOverride`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合は、値が空（""）であるとみなされます。

Windows版のみサポートしています。

「認証プロキシ爆発しろ！」がユーザーのプロキシ設定を書き換えた際に、
「次で始まるアドレスにはプロキシを使用しない」として適用すべきアドレスのリストを指定します。
また、「ローカルアドレスにはプロキシサーバーを使用しない」を適用する場合は、
アドレスのリストに`<local>`という文字列も追加します。

#### 例

プロキシ設定書き換え後の設定として、
「次で始まるアドレスにはプロキシを使用しない」を`*.example.com`と`*.example.local`とし、
かつ「ローカルアドレスにはプロキシサーバーを使用しない」を有効とする場合の例です。

&lt;と&gt;が特殊文字であるため、
オプションの値全体を引用符で括っていることに注意してください。

```
mape.exe /ProxyOverride:"*.example.com;*.example.local;<local>"
```


### RetryCount

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 整数
* 対応する設定: [Proxyオブジェクト](Reference_Settings.md#Proxyオブジェクト)の`RetryCount`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合、値が2であるとみなされます。

認証プロキシとの間の通信を何回やり直すかを指定します。

通信エラーの場合のリトライもありますが、
実際には認証失敗した場合のリトライ回数とみなしてよいでしょう。
「認証プロキシ爆発しろ！」と認証プロキシの間でユーザーに見えないやり取りがあったりもするので、
「だいたいこのくらいの回数リトライする」と思ってください。

#### 例

リトライ回数として7回を指定する例です。

```
mape.exe /RetryCount:7
```


### Save

* サポート対象
    * [x] Windows コマンド
    * [ ] Windows GUI
* 値の型: 値なし
* 対応する設定: なし
* 指定しなかった場合: コマンド実行種別を変更しません

コマンド版でのみ有効です。
GUI版では、GUIから設定を保存してください。（未実装だけど近日実装予定）

コマンドラインで指定した内容を設定ファイルに追加・保存し、終了します。
通信中継機能は起動しません。

このコマンドラインオプションは「認証プロキシ爆発しろ！」のコマンド実行種別を`SaveSettings`に変更します。

#### 例

設定ファイル中の`LogLevel`の設定を`Verbose`に変更する場合の例です。

```
mape.exe /Save /LogLevel:Verbose
```


### SettingsFile

* サポート対象
    * [x] Windows コマンド
    * [x] Windows GUI
* 値の型: 文字列
* 対応する設定: なし
* 指定しなかった場合: 通常の設定ファイルを読み込みます。

読み込む設定ファイルを明に指定します。
この場合、通常の設定ファイルは読み込まれません。

通常は使用しません。
デバッグやテスト目的で用意されています。

#### 例

設定ファイルを明に指定して起動する例です。

```
mape.exe /SettingsFile:"C:\Temp\test.json"
```

### Start

* サポート対象
    * [ ] Windows コマンド
    * [x] Windows GUI
* 値の型: "True", "False", なし,
* 対応する設定: [GUIオブジェクト](Reference_Settings.md#GUIオブジェクト)の`Start`プロパティ
* 指定しなかった場合: 設定ファイルの設定が適用されます。設定ファイルにも指定がない場合、値が"False"であるとみなされます。

「認証プロキシ爆発しろ！」起動後、自動的に中継を開始します。
この場合の詳細な挙動は[「認証プロキシ爆発しろ！」起動時に自動的に中継を開始させる場合の挙動](AdvancedTopics.md#「認証プロキシ爆発しろ！」起動時に自動的に中継を開始させる場合の挙動)を参照してください。

#### 例

「認証プロキシ爆発しろ！」を起動し、中継を開始させる例です。

```
mapegui.exe /Start

または

mapegui.exe /Start:True
```
