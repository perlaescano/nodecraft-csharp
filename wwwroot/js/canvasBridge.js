window.gameEngineBridge = {
    ctx: null,
    textarea: null,
    highlightEl: null,
    gutterEl: null,
    dotNetHelper: null,
    audioCtx: null,

    // =========================================================================
    // 1. BUILT-IN PIXEL-ART SPRITE LIBRARY (12x12 Grids)
    // =========================================================================
    spritePalettes: {
        "B": "#569cd6", // Blue
        "C": "#4fc1ff", // Cyan
        "G": "#4ec9b0", // Green / Mint
        "L": "#89d185", // Lime
        "Y": "#dcdcaa", // Gold / Yellow
        "O": "#ce9178", // Orange / Wood
        "R": "#f44747", // Red
        "P": "#c586c0", // Purple
        "W": "#ffffff", // White
        "S": "#9cdcfe", // Silver / Steel
        "D": "#5a5a5a", // Dark Gray
        "K": "#111111"  // Outline Black
    },

    sprites: {
        "knight": [
            "....SSSS....",
            "...SCCCCS...",
            "...SKWWKS...",
            "...SSSSSS...",
            "..BBRRRRBB..",
            ".SSBBBBBBSS.",
            ".SSBBBBBBSS.",
            ".WW.BBBB.WW.",
            "....SSSS....",
            "...SS..SS...",
            "...SS..SS...",
            "..DDD..DDD.."
        ],
        "slime": [
            "............",
            "............",
            "....GGGG....",
            "..GGGGGGGG..",
            ".GGGGGGGGGG.",
            ".GGWWGGWWGG.",
            ".GGKWGGKWGG.",
            "GGGGGGGGGGGG",
            "GGGGGGGGGGGG",
            "GGGGGGGGGGGG",
            ".GGGGGGGGGG.",
            "............"
        ],
        "coin": [
            "....YYYY....",
            "..YYYYYYYY..",
            ".YYYYWWYYYY.",
            ".YYYWWWWYYY.",
            "YYYYWWYYYYYY",
            "YYYYWWYYYYYY",
            "YYYYWWYYYYYY",
            "YYYYWWYYYYYY",
            ".YYYWWWWYYY.",
            ".YYYYWWYYYY.",
            "..YYYYYYYY..",
            "....YYYY...."
        ],
        "gem": [
            ".....CC.....",
            "....CCCC....",
            "...CCWWCC...",
            "..CCWWWWCC..",
            ".CCCCCCCCCC.",
            "CCCCCCCCCCCC",
            ".CCCCCCCCCC.",
            "..CCCCCCCC..",
            "...CCCCCC...",
            "....CCCC....",
            ".....CC.....",
            "............"
        ],
        "spaceship": [
            ".....WW.....",
            ".....WW.....",
            "....CCCC....",
            "....CWWC....",
            "...SSSSSS...",
            "..SSSSSSSS..",
            ".RRSSSSSSRR.",
            "RRSSSSSSSSRR",
            "RR.SSSSSS.RR",
            "...OO..OO...",
            "...YY..YY...",
            "............"
        ],
        "laser": [
            ".....WW.....",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RWWWR...",
            "....RRRRR...",
            ".....RRR....",
            "............"
        ],
        "star": [
            ".....YY.....",
            ".....YY.....",
            "....YYYY....",
            "YYYYYYYYYYYY",
            ".YYYYWWYYYY.",
            "..YYYYYYYY..",
            "..YYYYYYYY..",
            ".YYYYYYYYYY.",
            ".YYYY..YYYY.",
            "YYY......YYY",
            "YY........YY",
            "............"
        ],
        "heart": [
            "............",
            ".RRR....RRR.",
            "RRRRR..RRRRR",
            "RRWWRRRRRRRR",
            "RRWRRRRRRRRR",
            "RRRRRRRRRRRR",
            ".RRRRRRRRRR.",
            "..RRRRRRRR..",
            "...RRRRRR...",
            "....RRRR....",
            ".....RR.....",
            "............"
        ],
        "tree": [
            ".....GG.....",
            "....GGGG....",
            "...GGLGGG...",
            "..GGGGGGGG..",
            "...GGGGGG...",
            "..GGLGGGGG..",
            ".GGGGGGGGGG.",
            "GGGGGGGGGGGG",
            "....OOOO....",
            "....OOOO....",
            "....OOOO....",
            "...OOOOOO..."
        ],
        "crate": [
            "OOOOOOOOOOOO",
            "OYYYYYYYYYYO",
            "OYOOYYYYOOYO",
            "OYYOOYYOOYYO",
            "OYYYOOOOYYYO",
            "OYYYYOOYYYYO",
            "OYYYYOOYYYYO",
            "OYYYOOOOYYYO",
            "OYYOOYYOOYYO",
            "OYOOYYYYOOYO",
            "OYYYYYYYYYYO",
            "OOOOOOOOOOOO"
        ]
    },

    drawSpriteToContext: function (ctx, spriteName, x, y, w, h) {
        const grid = this.sprites[spriteName] || this.sprites["knight"];
        const rows = grid.length;
        const cols = grid[0].length;
        const cellW = w / cols;
        const cellH = h / rows;

        for (let r = 0; r < rows; r++) {
            const rowStr = grid[r];
            for (let c = 0; c < cols; c++) {
                const ch = rowStr[c];
                if (ch === ".") continue;
                ctx.fillStyle = this.spritePalettes[ch] || "#ffffff";
                ctx.fillRect(
                    Math.floor(x + c * cellW),
                    Math.floor(y + r * cellH),
                    Math.ceil(cellW),
                    Math.ceil(cellH)
                );
            }
        }
    },

    renderSpritePreview: function (canvasId, spriteName) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        const c = canvas.getContext("2d");
        c.clearRect(0, 0, canvas.width, canvas.height);
        this.drawSpriteToContext(c, spriteName, 4, 4, canvas.width - 8, canvas.height - 8);
    },

    // =========================================================================
    // 2. RETRO ARCADE SOUND SYNTHESIZER (WEB AUDIO API)
    // =========================================================================
    playSound: function (soundName) {
        try {
            const AudioContextClass = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextClass) return;
            if (!this.audioCtx) this.audioCtx = new AudioContextClass();
            if (this.audioCtx.state === "suspended") this.audioCtx.resume();

            const ctx = this.audioCtx;
            const now = ctx.currentTime;
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.connect(gain);
            gain.connect(ctx.destination);

            switch (soundName) {
                case "coin":
                    osc.type = "sine";
                    osc.frequency.setValueAtTime(987.77, now);
                    osc.frequency.setValueAtTime(1318.51, now + 0.08);
                    gain.gain.setValueAtTime(0.18, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.28);
                    osc.start(now);
                    osc.stop(now + 0.28);
                    break;
                case "jump":
                    osc.type = "square";
                    osc.frequency.setValueAtTime(180, now);
                    osc.frequency.exponentialRampToValueAtTime(560, now + 0.18);
                    gain.gain.setValueAtTime(0.12, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.19);
                    osc.start(now);
                    osc.stop(now + 0.19);
                    break;
                case "laser":
                    osc.type = "sawtooth";
                    osc.frequency.setValueAtTime(880, now);
                    osc.frequency.exponentialRampToValueAtTime(140, now + 0.14);
                    gain.gain.setValueAtTime(0.12, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.15);
                    osc.start(now);
                    osc.stop(now + 0.15);
                    break;
                case "powerup":
                    osc.type = "triangle";
                    osc.frequency.setValueAtTime(440, now);
                    osc.frequency.setValueAtTime(554.37, now + 0.07);
                    osc.frequency.setValueAtTime(659.25, now + 0.14);
                    osc.frequency.setValueAtTime(880, now + 0.21);
                    gain.gain.setValueAtTime(0.18, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.35);
                    osc.start(now);
                    osc.stop(now + 0.35);
                    break;
                case "explosion":
                case "hit":
                default:
                    osc.type = "sawtooth";
                    osc.frequency.setValueAtTime(150, now);
                    osc.frequency.exponentialRampToValueAtTime(42, now + 0.25);
                    gain.gain.setValueAtTime(0.18, now);
                    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.26);
                    osc.start(now);
                    osc.stop(now + 0.26);
                    break;
            }
        } catch (e) {
            // Ignore audio errors if browser blocks autoplay before user interaction
        }
    },

    // =========================================================================
    // 3. VS CODE DARK+ C# SYNTAX HIGHLIGHTER & EDITOR ENHANCEMENTS
    // =========================================================================
    initEditor: function (textareaId, highlightId, gutterId, dotNetHelper) {
        this.textarea = document.getElementById(textareaId);
        this.highlightEl = document.getElementById(highlightId);
        this.gutterEl = document.getElementById(gutterId);
        this.dotNetHelper = dotNetHelper;

        if (!this.textarea || !this.highlightEl || !this.gutterEl) return;

        const syncScroll = () => {
            const pre = this.highlightEl.parentElement;
            pre.scrollTop = this.textarea.scrollTop;
            pre.scrollLeft = this.textarea.scrollLeft;
            this.gutterEl.scrollTop = this.textarea.scrollTop;
        };

        this.textarea.addEventListener("scroll", syncScroll);
        this.textarea.addEventListener("input", () => {
            this.syncEditorHighlight();
            syncScroll();
        });

        // IDE Keybindings: Tab = 4 spaces, Enter = Auto-Indent, Ctrl+Enter = Run
        this.textarea.addEventListener("keydown", (e) => {
            if ((e.ctrlKey || e.metaKey) && e.key === "Enter") {
                e.preventDefault();
                dotNetHelper.invokeMethodAsync("OnEditorRunShortcut");
                return;
            }

            if (e.key === "Tab") {
                e.preventDefault();
                const start = this.textarea.selectionStart;
                const end = this.textarea.selectionEnd;
                const val = this.textarea.value;
                this.textarea.value = val.substring(0, start) + "    " + val.substring(end);
                this.textarea.selectionStart = this.textarea.selectionEnd = start + 4;
                this.syncEditorHighlight();
                dotNetHelper.invokeMethodAsync("OnEditorTextChanged", this.textarea.value);
            } else if (e.key === "Enter") {
                e.preventDefault();
                const start = this.textarea.selectionStart;
                const end = this.textarea.selectionEnd;
                const val = this.textarea.value;
                const lineStart = val.lastIndexOf("\n", start - 1) + 1;
                const currentLine = val.substring(lineStart, start);
                const leadingSpaces = currentLine.match(/^\s*/)[0];
                const extraIndent = currentLine.trimEnd().endsWith("{") ? "    " : "";
                const insertion = "\n" + leadingSpaces + extraIndent;

                this.textarea.value = val.substring(0, start) + insertion + val.substring(end);
                this.textarea.selectionStart = this.textarea.selectionEnd = start + insertion.length;
                this.syncEditorHighlight();
                syncScroll();
                dotNetHelper.invokeMethodAsync("OnEditorTextChanged", this.textarea.value);
            }
        });

        this.syncEditorHighlight();
    },

    insertSnippet: function (snippetText) {
        if (!this.textarea) return;
        const start = this.textarea.selectionStart ?? this.textarea.value.length;
        const end = this.textarea.selectionEnd ?? this.textarea.value.length;
        const val = this.textarea.value;

        this.textarea.value = val.substring(0, start) + snippetText + val.substring(end);
        this.textarea.focus();
        this.textarea.selectionStart = this.textarea.selectionEnd = start + snippetText.length;
        this.syncEditorHighlight();

        if (this.dotNetHelper) {
            this.dotNetHelper.invokeMethodAsync("OnEditorTextChanged", this.textarea.value);
        }
    },

    syncEditorHighlight: function () {
        if (!this.textarea || !this.highlightEl || !this.gutterEl) return;

        const code = this.textarea.value;
        const lineCount = code.split("\n").length;
        let gutterHtml = "";
        for (let i = 1; i <= lineCount; i++) {
            gutterHtml += i + "\n";
        }
        this.gutterEl.textContent = gutterHtml;

        const safeCode = code.endsWith("\n") ? code + " " : code;
        this.highlightEl.innerHTML = this.highlightCSharp(safeCode);
    },

    highlightCSharp: function (src) {
        const escapeHtml = (s) =>
            s.replace(/&/g, "&amp;")
             .replace(/</g, "&lt;")
             .replace(/>/g, "&gt;");

        const tokenRegex = /(\/\/[^\n]*|\/\*[\s\S]*?\*\/|\$?"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|\b\d+(?:\.\d+)?[fFdDmM]?\b|\b[A-Za-z_][A-Za-z0-9_]*\b)/g;

        const controlKeywords = new Set([
            "if", "else", "for", "foreach", "while", "do", "switch", "case", "default",
            "break", "continue", "return", "try", "catch", "finally", "throw"
        ]);

        const declKeywords = new Set([
            "using", "namespace", "public", "private", "protected", "internal",
            "class", "struct", "record", "interface", "enum", "static", "void",
            "override", "virtual", "abstract", "sealed", "readonly", "const",
            "new", "var", "true", "false", "null", "this", "base", "in", "out", "ref"
        ]);

        const builtinTypes = new Set([
            "int", "float", "double", "decimal", "string", "bool", "char", "byte", "long", "object",
            "Node", "Node2D", "Sprite2D", "Label", "Vector2", "Input", "Audio", "SceneTree",
            "List", "Dictionary", "Random", "Math", "MathF", "Console", "JsonSerializer", "Enumerable"
        ]);

        let result = "";
        let lastIndex = 0;
        let match;

        while ((match = tokenRegex.exec(src)) !== null) {
            if (match.index > lastIndex) {
                result += escapeHtml(src.slice(lastIndex, match.index));
            }

            const token = match[0];
            const nextChar = src[tokenRegex.lastIndex] || "";

            if (token.startsWith("//") || token.startsWith("/*")) {
                result += `<span class="tok-comment">${escapeHtml(token)}</span>`;
            } else if (token.startsWith("\"") || token.startsWith("$\"") || token.startsWith("'")) {
                result += `<span class="tok-string">${escapeHtml(token)}</span>`;
            } else if (/^\d/.test(token)) {
                result += `<span class="tok-number">${escapeHtml(token)}</span>`;
            } else if (controlKeywords.has(token)) {
                result += `<span class="tok-control">${token}</span>`;
            } else if (declKeywords.has(token)) {
                result += `<span class="tok-keyword">${token}</span>`;
            } else if (builtinTypes.has(token)) {
                result += `<span class="tok-type">${token}</span>`;
            } else if (nextChar === "(") {
                result += `<span class="tok-method">${token}</span>`;
            } else if (/^[A-Z]/.test(token)) {
                result += `<span class="tok-type">${token}</span>`;
            } else {
                result += `<span class="tok-ident">${token}</span>`;
            }

            lastIndex = tokenRegex.lastIndex;
        }

        if (lastIndex < src.length) {
            result += escapeHtml(src.slice(lastIndex));
        }

        return result;
    },

    // =========================================================================
    // 4. HTML5 CANVAS BATCHED 60 FPS RENDERER & INPUT CAPTURE
    // =========================================================================
    initCanvas: function (canvasId, dotNetHelper) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;
        this.ctx = canvas.getContext("2d");

        canvas.addEventListener("keydown", (e) => {
            if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " "].includes(e.key)) {
                e.preventDefault();
            }
            dotNetHelper.invokeMethodAsync("OnKeyChanged", e.key, true);
        });

        canvas.addEventListener("keyup", (e) => {
            dotNetHelper.invokeMethodAsync("OnKeyChanged", e.key, false);
        });

        canvas.addEventListener("blur", () => {
            ["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", "w", "a", "s", "d", " ", "Enter"].forEach(k => {
                dotNetHelper.invokeMethodAsync("OnKeyChanged", k, false);
            });
        });
    },

    renderFrame: function (commands) {
        if (!this.ctx || !commands) return;
        const ctx = this.ctx;

        for (let i = 0; i < commands.length; i++) {
            const cmd = commands[i];
            switch (cmd.type) {
                case "clear":
                    ctx.fillStyle = cmd.color;
                    ctx.fillRect(0, 0, ctx.canvas.width, ctx.canvas.height);
                    break;
                case "rect":
                    ctx.fillStyle = cmd.color;
                    ctx.fillRect(cmd.x, cmd.y, cmd.w, cmd.h);
                    break;
                case "sprite":
                    this.drawSpriteToContext(ctx, cmd.text || "knight", cmd.x, cmd.y, cmd.w, cmd.h);
                    break;
                case "circle":
                    ctx.fillStyle = cmd.color;
                    ctx.beginPath();
                    ctx.arc(cmd.x, cmd.y, cmd.w, 0, Math.PI * 2);
                    ctx.fill();
                    break;
                case "line":
                    ctx.strokeStyle = cmd.color;
                    ctx.lineWidth = cmd.fontSize || 2;
                    ctx.beginPath();
                    ctx.moveTo(cmd.x, cmd.y);
                    ctx.lineTo(cmd.w, cmd.h);
                    ctx.stroke();
                    break;
                case "text":
                    ctx.fillStyle = cmd.color;
                    ctx.font = `${cmd.fontSize || 16}px Consolas, monospace`;
                    ctx.fillText(cmd.text || "", cmd.x, cmd.y);
                    break;
                case "sound":
                    this.playSound(cmd.text || "coin");
                    break;
            }
        }
    },

    downloadTextFile: function (filename, content) {
        const blob = new Blob([content], { type: "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const a = document.createElement("a");
        a.href = url;
        a.download = filename;
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }
};
