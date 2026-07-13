# References

Pasta para bibliotecas de terceiros que **não podem ser redistribuídas** junto
com o repositório (por licença), referenciadas via `HintPath` relativo no
`.csproj`.

## SAP .NET Connector (NCo)

Software proprietário licenciado pela SAP. Copie os seguintes arquivos para
esta pasta antes de compilar o projeto `AutoCheck Mechanical`:

- `sapnco.dll`
- `sapnco_utils.dll`

Versão usada pelo projeto: `3.1.0.42`, build **AMD64/x64** (o projeto compila
como `AnyCPU`, igual o WBC real -- não use a build x86, ela não existe/não é
usada pelo WBC de produção). Obtenha-os na área de downloads da SAP (SAP
Support Portal → SAP NCo 3.0) ou copie de outra instalação já licenciada
(ex.: pasta do projeto WBC).

## Wau.Util / Weg.Iceberg

Bibliotecas internas proprietárias da WEG. Copie os seguintes arquivos:

- `Wau.Util.dll`
- `Weg.Iceberg.dll`

Usadas pra chamar o Web Service SOA `ITF_O_S_DOCUMENT_OUTPUT` do mesmo jeito
que o WBC e o WAU Factory Viewer fazem (`Weg.Iceberg.Infrastructure.Uddi.
SoapClientFactory` + `Wau.Util.Services.SapServices.GetServiceCredential()`).

Esta pasta (e os `.dll` dentro dela) é ignorada pelo git -- veja `.gitignore`
na raiz do repositório.
