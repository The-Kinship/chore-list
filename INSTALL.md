# Setting up Rafter Cat's Chore List

## 1. Make the GitHub repo
1. On GitHub (signed in as The-Kinship), make a new **public** repo called `chore-list`. Leave it empty.
2. Upload everything in this zip, including the hidden `.github` folder.
   If `.github` doesn't show up after uploading, make it by hand: **Add file > Create new file**, type the name
   `.github/workflows/build.yml`, paste in that file's contents, and commit.
3. Go to **Settings > Actions > General > Workflow permissions**, choose **Read and write permissions**, Save.
   (The build needs this to publish the plugin and update repo.json.)
4. Open the **Actions** tab. "Build plugin" starts by itself (or press **Run workflow**). Wait for the green check.
   A red X? Open it and send a screenshot of the failing step.

## 2. Install it in game
1. `/xlsettings` > **Experimental** > **Custom Plugin Repositories**.
2. Paste `https://raw.githubusercontent.com/The-Kinship/chore-list/main/repo.json`, press +, then Save.
3. `/xlplugins`, search **Chore List**, Install.
4. Type `/chores`.

## 3. Try it
- Type "Check retainers" in the top box, press Enter. It's on every character's list.
- Type something in the bottom box. It's only on this character's list.
- Tick one, close the game, come back: it's still ticked.
- Log in on another character: the top list is the same, the bottom one is that character's own.
- Right-click a chore > Delete.
