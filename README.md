# sumile

シフト管理アプリです。C# と ASP.NET Core で開発しています。

## 要件
- .NET SDK 8.x
- PostgreSQL（ローカルまたは接続可能な DB）

## 起動手順（Windows / PowerShell）

1. 依存パッケージの復元とビルド

```powershell
dotnet restore
dotnet build
```

2. 環境変数（`.env`）の準備

プロジェクトルートに `.env` ファイルを作成し、Postgres の接続文字列を設定します。

例:

```
DB_CONNECTION_STRING=Host=localhost;Port=5432;Database=sumile;Username=youruser;Password=yourpassword
```

アプリは起動時に `.env` を読み込み、`DB_CONNECTION_STRING` から接続情報を取得します。

3. EF Core ツール（未インストールの場合）

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

4. マイグレーションを適用してデータベースを作成/更新

```powershell
dotnet ef database update
```

5. アプリを起動

```powershell
dotnet run
```

起動後に表示される URL（例: `https://localhost:5001`）をブラウザで開いてください。

## Visual Studio を使う場合
- ソリューション `sumile.sln` を開く
- デフォルトプロファイル（Kestrel）で実行（F5 または Ctrl+F5）

## トラブルシュート
- `DB_CONNECTION_STRING` が見つからないエラー → `.env` を作成するか、PowerShell で環境変数を設定:

```powershell
$env:DB_CONNECTION_STRING="Host=...;Username=...;Password=..."
```

- PostgreSQL が起動していることを確認してください（通常ポート 5432）。
- HTTPS 証明書で問題が出る場合:

```powershell
dotnet dev-certs https --trust
```

不明点や環境に合わせた接続文字列の例が必要であれば教えてください。
# sumile
