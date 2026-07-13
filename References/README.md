# References

Pasta para bibliotecas de terceiros que **não podem ser redistribuídas** junto
com o repositório (por licença), referenciadas via `HintPath` relativo no
`.csproj`.

## SAP .NET Connector (NCo)

Software proprietário licenciado pela SAP. Copie os seguintes arquivos para
esta pasta antes de compilar o projeto `AutoCheck Mechanical`:

- `sapnco.dll`
- `sapnco_utils.dll`

Versão usada pelo projeto: `3.1.0.42` (x86). Obtenha-os na área de downloads
da SAP (SAP Support Portal → SAP NCo 3.0) ou copie de outra instalação já
licenciada (ex.: pasta `References\` do projeto WBC).

Esta pasta (e os `.dll` dentro dela) é ignorada pelo git -- veja `.gitignore`
na raiz do repositório.
