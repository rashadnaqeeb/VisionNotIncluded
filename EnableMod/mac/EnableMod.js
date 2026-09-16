// EnableMod.js - Enables Vision Not Included in the game's mod config on macOS.
//
// Compiled into EnableMod.app by package.sh (JavaScript for Automation applet).
// Double-clicked, it shows dialogs like the Windows EnableMod.exe. From a shell
// it runs headless and prints a status word instead:
//
//   osascript -l JavaScript EnableMod.js --quiet   -> OK | NOT_FOUND | NO_FILE
//
// build.sh uses the headless mode to keep the mod enabled after each deploy.

ObjC.import('Foundation');

const TITLE = 'Vision Not Included';
const WORKSHOP_URL = 'https://steamcommunity.com/sharedfiles/filedetails/?id=3683507975';
const STEAM_LAUNCH_URL = 'steam://rungameid/457140';
const MODS_JSON = $.NSHomeDirectory().js
	+ '/Library/Application Support/unity.Klei.Oxygen Not Included/mods/mods.json';

const NOT_FOUND_MESSAGE =
	'Could not find the Vision Not Included mod config.\n\n'
	+ 'If you haven\'t subscribed yet, click Open Workshop Page.\n'
	+ 'If you have subscribed, launch the game once, close it, then click Try Again.';

function fileExists(path) {
	return $.NSFileManager.defaultManager.fileExistsAtPath(path);
}

function readFile(path) {
	const error = Ref();
	const text = $.NSString.stringWithContentsOfFileEncodingError(path, $.NSUTF8StringEncoding, error);
	if (text.isNil())
		throw new Error(error[0].localizedDescription.js);
	return text.js;
}

// Writes UTF-8 without a BOM; a BOM makes the game discard the whole file.
function writeFile(path, text) {
	const error = Ref();
	const ok = $(text).writeToFileAtomicallyEncodingError(path, true, $.NSUTF8StringEncoding, error);
	if (!ok)
		throw new Error(error[0].localizedDescription.js);
}

function isOniAccess(mod) {
	if (mod.staticID === 'OniAccess') return true;
	return typeof mod.label === 'object' && mod.label !== null && mod.label.id === 'OniAccess';
}

// Returns true when the mod entry was found and enabled, false when the
// game has not discovered the mod yet.
function enableMod(path) {
	const root = JSON.parse(readFile(path));
	if (!Array.isArray(root.mods)) return false;

	const mod = root.mods.find(isOniAccess);
	if (!mod) return false;

	mod.enabled = true;
	mod.enabledForDlc = ['', 'EXPANSION1_ID'];
	mod.crash_count = 0;
	mod.status = 1; // Status.Installed
	// A crash while mods were loading leaves this set, and the next boot then
	// enters mod safe mode and disables every mod again.
	root.mod_load_in_progress = false;

	writeFile(path, JSON.stringify(root));
	return true;
}

function tryEnable() {
	if (!fileExists(MODS_JSON)) return 'NO_FILE';
	return enableMod(MODS_JSON) ? 'OK' : 'NOT_FOUND';
}

// The cancel button raises "User canceled" (-128); treat it as that button.
function ask(app, message, buttons, defaultButton, cancelButton) {
	try {
		return app.displayDialog(message, {
			withTitle: TITLE,
			buttons: buttons,
			defaultButton: defaultButton,
			cancelButton: cancelButton,
		}).buttonReturned;
	} catch (e) {
		return cancelButton;
	}
}

function run(argv) {
	argv = argv || [];
	if (argv.indexOf('--quiet') !== -1)
		return tryEnable();

	const app = Application.currentApplication();
	app.includeStandardAdditions = true;

	while (true) {
		let result;
		try {
			result = tryEnable();
		} catch (e) {
			app.displayDialog('Failed to update mod config: ' + e.message, {
				withTitle: TITLE,
				buttons: ['OK'],
				defaultButton: 'OK',
				withIcon: 'stop',
			});
			return;
		}

		if (result !== 'OK') {
			const choice = ask(app, NOT_FOUND_MESSAGE,
				['Close', 'Try Again', 'Open Workshop Page'], 'Try Again', 'Close');
			if (choice === 'Open Workshop Page')
				app.openLocation(WORKSHOP_URL);
			else if (choice !== 'Try Again')
				return;
			continue;
		}

		const launch = ask(app, 'Vision Not Included enabled. Launch the game?',
			['No', 'Yes'], 'Yes', 'No');
		if (launch === 'Yes')
			app.openLocation(STEAM_LAUNCH_URL);
		return;
	}
}
