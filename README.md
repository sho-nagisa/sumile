# sumile

sumile は、シフト提出・自動割り当て・交換申請・管理者編集を扱うシフト管理アプリです。
ASP.NET Core MVC と PostgreSQL を使って開発しています。

## 主な機能

- ユーザー登録、ログイン、マイページ
- 募集期間ごとのシフト提出
- シフト一覧、提出状況の確認
- シフト交換申請、応募、承認、却下
- 管理者による募集期間、必要人数、シフト割り当ての管理
- 自動シフト割り当て
- 編集ログの確認
- PDF 出力

## 技術スタック

- .NET 8 / ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core 8
- PostgreSQL / Npgsql
- Bootstrap / jQuery
- PdfSharpCore / SkiaSharp
- xUnit / EF Core InMemory

## ディレクトリ構成

| パス | 内容 |
| --- | --- |
| `Controllers/` | MVC コントローラー |
| `Services/` | シフト、交換、管理画面などの業務ロジック |
| `Models/` | Entity と画面入力モデル |
| `ViewModels/` | 画面表示用モデル |
| `Data/` | `ApplicationDbContext` |
| `Migrations/` | EF Core マイグレーション |
| `Views/` | Razor ビュー |
| `wwwroot/` | CSS、JavaScript、フォント、PDF 用画像など |
| `sumile.Tests/` | xUnit テスト |

## 必要なもの

- .NET SDK 8.x
- PostgreSQL
- EF Core CLI ツール（マイグレーション操作をする場合）

EF Core CLI が未インストールの場合:

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

## ローカル起動手順

1. 依存パッケージを復元します。

```powershell
dotnet restore
```

2. プロジェクトルートに `.env` を作成し、PostgreSQL の接続文字列を設定します。

```env
DB_CONNECTION_STRING=Host=localhost;Port=5432;Database=sumile;Username=youruser;Password=yourpassword
```

3. ビルドします。

```powershell
dotnet build
```

4. マイグレーションを適用してデータベースを作成または更新します。

```powershell
dotnet ef database update
```

5. アプリを起動します。

```powershell
dotnet run --launch-profile https
```

起動後、通常は次の URL で開けます。

- HTTPS: `https://localhost:7033`
- HTTP: `http://localhost:5039`

`dotnet run` の出力に別の URL が表示された場合は、表示された URL を開いてください。

## Visual Studio で起動する場合

1. `sumile.sln` を開きます。
2. 起動プロファイルで `https` または `IIS Express` を選びます。
3. F5 または Ctrl+F5 で実行します。

## テスト

テストは `sumile.Tests` プロジェクトにあります。
テストでは EF Core InMemory を使うため、PostgreSQL や `.env` は不要です。

```powershell
dotnet test .\sumile.Tests\sumile.Tests.csproj
```

ビルド済みの状態でテストだけ再実行する場合:

```powershell
dotnet test .\sumile.Tests\sumile.Tests.csproj --no-build
```

## マイグレーション

モデルを変更したら、マイグレーションを追加してから DB に適用します。

```powershell
dotnet ef migrations add MigrationName
dotnet ef database update
```

## 環境変数

| 変数名 | 内容 |
| --- | --- |
| `DB_CONNECTION_STRING` | PostgreSQL の接続文字列。起動時に `.env` または環境変数から読み込まれます。 |

`.env` はローカル環境用です。接続情報やパスワードをコミットしないでください。

## トラブルシュート

`DB_CONNECTION_STRING` が見つからない場合は、`.env` を作成するか、PowerShell で環境変数を設定してください。

```powershell
$env:DB_CONNECTION_STRING="Host=localhost;Port=5432;Database=sumile;Username=youruser;Password=yourpassword"
```

`dotnet ef` が見つからない場合は、EF Core CLI をインストールしてください。

```powershell
dotnet tool install --global dotnet-ef --version 8.*
```

HTTPS 証明書で警告やエラーが出る場合:

```powershell
dotnet dev-certs https --trust
```

PostgreSQL に接続できない場合は、PostgreSQL が起動していること、接続文字列の `Host`、`Port`、`Database`、`Username`、`Password` が正しいことを確認してください。
