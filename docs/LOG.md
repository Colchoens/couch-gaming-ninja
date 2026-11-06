# Couch Gaming Ninja – log do projeto

Registro do que foi feito, em ordem. Datas no horário de Brasília.

## 06/11/2026 – conversa "Retrobat e Rocket League" (madrugada e manhã)

**Problema de origem:** Rocket League (versão Epic) no PC da sala (steam-machine) não aceitava o controle Switch Pro pela Steam.

- Testadas as saídas comuns. O Epic Launcher dentro da Steam não serve, porque a Steam engancha no launcher e não no jogo. O "Add to Steam" do Heroic também não: com o Heroic aberto, o processo iniciado pela Steam repassa o pedido e fecha, e a Steam perde o rastro. Com o Heroic fechado, o `--no-gui` não abria o jogo.
- **Solução manual que funcionou:** atalho da Steam com destino `powershell.exe` oculto. Ele chama o Legendary (`legendary launch Sugar --skip-version-check`) e espera o processo `RocketLeague` fechar. A Steam acompanha o PowerShell, aplica a Entrada Steam e mostra "Jogando" até o fim.
- Legendary **0.20.34** fixado, porque o login em janela da 0.21.1 quebra no Windows (pythonnet/Python 3.14).
- RetroBat: `.url` com `steam://rungameid/<id>` em `C:\Games\Retrobat\roms\windows`. O ID vem de CRC32("exe entre aspas" + nome) | 0x80000000, deslocado 32 bits, | 0x02000000.
- RetroBat configurado para abrir pela Steam junto com o Windows (`Retrobat (Steam).url` na pasta de inicialização).
- **Decisão:** transformar a solução num app gratuito e open source (MIT), com o nome "Couch Gaming Ninja", no GitHub do Colchoens. O repositório é próprio, não fork, com créditos ao Legendary e ao Slipstream.

### v0.1.0 (construída na nuvem)
- C# / WinForms, .NET Framework 4.8, `.exe` único de ~60 KB, compilado com Mono (`build.sh`), com testes de VDF e de appid.
- Funções: baixa o Legendary, faz o login pela janela da Epic, lista os jogos, acha instalações do Epic Launcher e do Heroic ("Locate folder…" para outras pastas), escreve `shortcuts.vdf` e liga a Entrada Steam em `localconfig.vdf` (com backup), fecha e reabre a Steam, cria o `.url` do RetroBat e tem "Remove everything".
- Avisos na tela para: sem Steam, Steam sem login e sem jogos.
- README, licença MIT, print e build automático pelo GitHub Actions.
- Teste no caio-pc: login e logout ok. Dois ajustes de interface: limpar a mensagem velha na tela de login e deixar o "Apply to Steam" cinza quando inativo.

## 06/11/2026 – conversa no projeto Qegen (08:40–09:20)

- O código-fonte tinha ficado só na outra conversa. Primeiro veio o `.exe` corrigido (08:36); o `couch-gaming-ninja-source.zip` chegou depois.
- **Teste real da v0.1.0 no PC da sala (steam-machine):**
  - Login Epic (Colchoens) ok; Rocket League encontrado em `D:\rocketleague` (instalação do Heroic).
  - RetroBat detectado sozinho em `C:\Games\RetroBat\roms\windows`.
  - "Apply to Steam" ok: atalho com Entrada Steam, Switch Pro funcionando, "Jogando" até fechar, e abrindo pelo RetroBat também. ✅
  - "Remove everything" ok: atalho saiu da Steam, `.url` saiu do RetroBat, pasta do app apagada e logout feito. ✅
- **Bug grave encontrado:** o Apply gravou o `Rocket League®.url` **por cima** do atalho do RetroBat feito à mão, sem perguntar, e o Remove apagou esse arquivo. O RetroBat ficou sem Rocket League. Reparo manual: criar o atalho pela Steam (Gerenciar → Adicionar atalho à área de trabalho) e mover para `roms\windows`.
- **Bugs menores:** durante o Remove, a lista continuava mostrando "In Steam ✓".
- **Pedido do usuário:** o app deve ser o mais portátil possível e perguntar onde instalar, com o padrão como sugestão. Program Files ficou de fora, porque exige administrador e a Steam abre os jogos sem isso.
- Tentativa de criar o repositório no GitHub por esta sessão: bloqueada, porque a sessão não pode criar repositórios. O usuário precisa criar `Colchoens/couch-gaming-ninja` (privado) em github.com/new.
- Código, log e roadmap salvos no projeto Qegen do Claude (pasta `couch-gaming-ninja/`), para qualquer conversa do projeto continuar sem depender de zip.

### v0.1.1
- **Escolha do local de instalação** na primeira execução: recomendado (`%LOCALAPPDATA%\CouchGamingNinja`), portátil (ao lado do `.exe`) ou outra pasta. Program Files, Windows e pastas temporárias são recusados com explicação.
- Tudo numa pasta só: o app se copia para lá, junto com o Legendary, o login, as configurações e os backups. A pasta fica registrada em `HKCU\Software\CouchGamingNinja`. Se o `.exe` roda de dentro da própria pasta, ela é usada mesmo sem o registro (modo portátil).
- Atalho no Menu Iniciar (opcional, marcado por padrão).
- **Move app folder…:** copia a pasta, reescreve os atalhos da Steam (o appid não muda, então os links do RetroBat continuam valendo), reabre a partir da pasta nova e apaga a antiga.
- Rodar um `.exe` mais novo de qualquer lugar atualiza a cópia instalada e abre ela.
- **Atalho que avisa:** se a pasta do app sumir, o atalho da Steam mostra "Open Couch Gaming Ninja to repair" em vez de não fazer nada. O app detecta atalhos apontando para pasta antiga e o Apply repara.
- **RetroBat seguro:** os `.url` do app levam a seção `[CouchGamingNinja]`. Se já existir um arquivo com o mesmo nome que não seja do app, ele pergunta: substituir (com backup), manter os dois ou deixar como está. O Remove só apaga arquivos do app e restaura os backups.
- **Remove everything:** mostra o progresso, remove o atalho do Menu Iniciar e o registro, e apaga a própria pasta depois de fechar.
- Os dois ajustes da v0.1.0 (mensagem velha no login e botão cinza) reaplicados, porque o zip do código era anterior a eles.
- 30 testes automáticos passando, incluindo o cenário do bug do RetroBat e a mudança de pasta.

### Ajustes da v0.1.1 durante o teste no PC da sala (09:20–09:50)
- **Instalado em `D:\CouchGamingNinja`** (escolha do usuário, com atalho no Menu Iniciar).
- **Bolinha vazia na tela de instalação:** a opção "Portable" desativada ficava com o texto invisível (cinza do Windows sobre fundo escuro). Agora, quando ela não está disponível (app rodando de dentro do zip), aparece uma frase legível no lugar. No "Move app folder…", a pasta atual não é mais desativada: aparece "(current)", e escolher ela mostra um aviso.
- **Avisos:** o usuário não quer nenhum aviso antes da tela de login. A instalação e a atualização ficaram silenciosas. O único resumo vem no fim do **Apply to Steam** ("All set"), listando jogo por jogo o que foi feito na Steam e no RetroBat, e depois **fecha o app**. Se algo falhar, o resumo mostra o problema e a janela fica aberta. "Move app folder…" e "Remove everything" também terminam com resumo.
- **Atualização não acontecia:** ao abrir um `.exe` novo, o app comparava só o número da versão (os dois eram 0.1.1) e abria a cópia antiga do D:. Agora ele substitui a cópia instalada quando a versão é maior ou quando é a mesma versão com arquivo diferente. Uma versão mais antiga nunca substitui uma mais nova.
- **Apply cinza sem explicação:** a linha de status agora diz "Everything is already set up in Steam. Nothing to apply." ou "Click Add to Steam on a game, then Apply.".
- **Jogo na Steam, mas não importado no Legendary do app:** agora conta como mudança pendente e o Apply fica liberado para importar e consertar.
- **Travamento no Apply:** o Legendary perguntava "importar DLCs? [S/n]" numa janela escondida e ficava esperando. Agora o import usa `--skip-dlcs`, o stdin do Legendary é fechado (qualquer pergunta termina na hora) e há tempo limite (3 min no import, 15 min no login, 5 min no resto).
- **Status parado em "Starting Steam…"** depois de terminar: corrigido.
- **Lista que pula no clique:** a lista rolava sozinha para mostrar o botão focado, e o clique podia se perder. A lista foi trocada por uma que não pula.
- O usuário apagou o Heroic e os atalhos antigos da Steam. **Apply com a v0.1.1 em `D:\CouchGamingNinja`: ok.** Resumo "All set", import sem travar. Falta confirmar o jogo abrindo pela Steam com o Switch Pro.
- **Instalação limpa com a build final (09:50–09:53): ok.** O usuário removeu tudo e reinstalou do zero. Segundo ele, "parece que deu tudo certo". ✅

## 06/11/2026 – GitHub
- Repositório `Colchoens/couch-gaming-ninja` criado como **privado** pelo navegador do usuário (Chrome), com a descrição "Play your Epic games from Steam with any controller. Windows."
- Claude GitHub App instalado só nesse repositório.
- Primeiro commit com o código da v0.1.1 (o mesmo da pasta `couch-gaming-ninja/` do projeto Qegen). A partir daqui, o repositório é a fonte do código.
