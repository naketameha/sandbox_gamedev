function generateScriptableAsset() {
  console.log("=== generateScriptableAsset開始 ===");
  
  try {
    const spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
    const baseName = spreadsheet.getName();
    
    // メインシートからデータ取得
    const mainSheet = spreadsheet.getSheets()[0];
    const data = mainSheet.getDataRange().getValues();
    
    const fieldNames = data[0]; // ID,NextID,state...
    const types = data[1];      // string,string,string...
    
    // 設定シートからGUID取得
    let scriptGUID = "PLACEHOLDER_GUID";
    try {
      const settingsSheet = spreadsheet.getSheetByName("settings");
      if (settingsSheet) {
        const settingsData = settingsSheet.getDataRange().getValues();
        for (let row of settingsData) {
          if (row[0] === "クラス設定用の.csのGUID") {
            scriptGUID = row[1];
            break;
          }
        }
        console.log(`設定から取得したGUID: ${scriptGUID}`);
      }
    } catch (e) {
      console.log("設定シートが見つからないため、プレースホルダーを使用");
    }
    
    const assetCode = generateDialogueAsset_Internal(baseName, data.slice(2), types, fieldNames, scriptGUID, mainSheet);
    const fileName = `${baseName}_00.asset`;
    
    saveToFolder_Internal(fileName, assetCode);
    console.log(`✅ アセットファイル生成完了: ${fileName}`);
    
  } catch (error) {
    console.error(`❌ エラー: ${error.message}`);
    console.error(error.stack);
  }
}

function generateDialogueAsset_Internal(baseName, dataRows, types, fieldNames, scriptGUID, mainSheet) {
  console.log(`generateDialogueAsset_Internal開始 - baseName: "${baseName}"`);
  
  const className = `${baseName}DataScriptable`;
  const propertyName = `${baseName.toLowerCase()}Items`;
  
  let yaml = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${scriptGUID}, type: 3}
  m_Name: "${baseName}_00"
  m_EditorClassIdentifier: Assembly-CSharp::${className}
  ${propertyName}:`;

  dataRows.forEach((row, rowIndex) => {
    if (!row) {
      return;
    }
    
    // A列（row[0]）が"//"で始まる場合はコメント行としてスキップ
    if (row[0] !== null && row[0] !== undefined && row[0] !== "") {
      const firstCell = String(row[0]).trim();
      if (firstCell.startsWith('//')) {
        console.log(`コメント行をスキップ: 行${rowIndex + 3} - ${firstCell}`);
        return;
      }
    }
    
    // B列以降に何かデータがあるかチェック（全部空だったらスキップ）
    const hasData = row.slice(1).some(cell => cell !== null && cell !== undefined && cell !== "");
    if (!hasData) {
      return;
    }
    
    yaml += `\n  - `;
    
    let isFirst = true;
    for (let i = 0; i < fieldNames.length && i < row.length; i++) {
      if (fieldNames[i]) {
        const currentType = types[i] || 'string';
        let value;
        
        // string型の場合のみリッチテキスト処理を適用
        if (currentType.toLowerCase() === 'string') {
          try {
            const cellRange = mainSheet.getRange(rowIndex + 3, i + 1);
            const richTextValue = cellRange.getRichTextValue();
            
            if (richTextValue && richTextValue.getText()) {
              value = convertToUnityRichTextOptimized(richTextValue);
            } else {
              value = formatValueForYAML_Internal(row[i], currentType);
            }
          } catch (e) {
            console.log(`リッチテキスト取得エラー(${rowIndex + 3}, ${i + 1}): ${e.message}`);
            value = formatValueForYAML_Internal(row[i], currentType);
          }
        } else {
          value = formatValueForYAML_Internal(row[i], currentType);
        }
        
        if (isFirst) {
          yaml += `${fieldNames[i]}: ${value}`;
          isFirst = false;
        } else {
          yaml += `\n    ${fieldNames[i]}: ${value}`;
        }
      }
    }
  });

  return yaml;
}

function convertToUnityRichTextOptimized(richTextValue) {
  const text = richTextValue.getText();
  if (!text) return '""';
  
  let result = "";
  let currentTags = [];
  let pendingText = "";
  
  function flushPendingText() {
    if (pendingText) {
      let wrappedText = pendingText;
      
      // タグを適用（ネストを考慮した順序）
      const openTags = [];
      const closeTags = [];
      
      for (const tag of currentTags) {
        const tagName = tag.split('=')[0];
        openTags.push(`<${tag}>`);
        closeTags.unshift(`</${tagName}>`); // 逆順でクローズ
      }
      
      wrappedText = openTags.join('') + wrappedText + closeTags.join('');
      result += wrappedText;
      pendingText = "";
    }
  }
  
  for (let i = 0; i < text.length; i++) {
    const char = text[i];
    
    // 改行は特別処理（スタイル関係なし）
    if (char === '\n') {
      flushPendingText();
      result += '\\n';
      continue;
    }
    
    const textStyle = richTextValue.getTextStyle(i, i + 1);
    
    // 現在の文字のスタイルタグを構築
    const newTags = [];
    
    if (textStyle.isBold()) newTags.push('b');
    if (textStyle.isItalic()) newTags.push('i');
    if (textStyle.isStrikethrough()) newTags.push('s');
    
    const foregroundColor = textStyle.getForegroundColor();
    if (foregroundColor && foregroundColor !== '#000000') {
      newTags.push(`color=${foregroundColor}`);
    }
    
    const fontSize = textStyle.getFontSize();
    if (fontSize && fontSize !== 10) {
      newTags.push(`size=${fontSize}`);
    }
    
    const fontFamily = textStyle.getFontFamily();
    if (fontFamily && fontFamily !== 'Arial') {
      // フォント名のダブルクォートをエスケープ
      newTags.push(`font=\\"${fontFamily}\\"`);
    }
    
    // スタイルが変わった場合、前のテキストをフラッシュ
    if (JSON.stringify(newTags.sort()) !== JSON.stringify(currentTags.sort())) {
      flushPendingText();
      currentTags = newTags;
    }
    
    pendingText += char;
  }
  
  // 残りのテキストをフラッシュ
  flushPendingText();
  
  // Unicode エスケープして引用符で囲む
  return `"${escapeUnicode_Internal(result)}"`;
}

// 以下、既存の関数たち...
function formatValueForYAML_Internal(value, type) {
  if (value === null || value === undefined || value === "") {
    switch ((type || '').toLowerCase()) {
      case 'string': return '""';
      case 'bool': return '0';
      case 'float': return '0';
      case 'int': 
      case 'integer': return '0';
      case 'vector3': return '{x: 0, y: 0, z: 0}';
      default: return '""';
    }
  }
  
  switch ((type || '').toLowerCase()) {
    case 'string':
      return `"${escapeUnicode_Internal(String(value))}"`;
    case 'bool':
      const boolStr = String(value).toLowerCase();
      return (boolStr === 'true' || boolStr === '1') ? '1' : '0';
    case 'float':
      return parseFloat(value) || 0;
    case 'int':
    case 'integer':
      return parseInt(value) || 0;
    case 'vector3':
      if (typeof value === 'string' && value.includes(',')) {
        const parts = value.split(',').map(s => s.trim());
        const x = parseFloat(parts[0]) || 0;
        const y = parseFloat(parts[1]) || 0;
        const z = parseFloat(parts[2]) || 0;
        return `{x: ${x}, y: ${y}, z: ${z}}`;
      }
      return '{x: 0, y: 0, z: 0}';
    default:
      return `"${escapeUnicode_Internal(String(value))}"`;
  }
}

function escapeUnicode_Internal(str) {
  return String(str).replace(/[\u0080-\uFFFF]/g, function(match) {
    return "\\u" + ("0000" + match.charCodeAt(0).toString(16)).substr(-4);
  });
}

function saveToFolder_Internal(fileName, content) {
  const spreadsheetFile = DriveApp.getFileById(SpreadsheetApp.getActiveSpreadsheet().getId());
  const parentFolder = spreadsheetFile.getParents().next();
  
  const existingFiles = parentFolder.getFilesByName(fileName);
  
  if (existingFiles.hasNext()) {
    existingFiles.next().setContent(content);
    console.log(`ファイル上書き: ${fileName}`);
  } else {
    parentFolder.createFile(fileName, content);
    console.log(`新規ファイル作成: ${fileName}`);
  }
}