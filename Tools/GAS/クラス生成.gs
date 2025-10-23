function generateScriptableClass() {
  console.log("=== generateScriptableClass開始 ===");
  
  try {
    const spreadsheet = SpreadsheetApp.getActiveSpreadsheet();
    
    // メインシートからデータ取得
    const mainSheet = spreadsheet.getSheets()[0]; // 最初のシート
    const data = mainSheet.getDataRange().getValues();
    
    const fieldNames = data[0]; // ID,NextID,state,CharacterID...
    const types = data[1];      // string,string,string,Integer...
    
    console.log(`フィールド名: [${fieldNames}]`);
    console.log(`型: [${types}]`);
    
    // 設定シートから情報取得
    let className = "DialogueScriptableClasses"; // デフォルト
    try {
      const settingsSheet = spreadsheet.getSheetByName("settings");
      if (settingsSheet) {
        const settingsData = settingsSheet.getDataRange().getValues();
        for (let row of settingsData) {
          if (row[0] === "クラス設定用の.csの名前") {
            className = row[1];
            break;
          }
        }
        console.log(`設定から取得したクラス名: ${className}`);
      }
    } catch (e) {
      console.log("設定シートが見つからないため、デフォルト名を使用");
    }
    
    // スプレッドシート名をベースクラス名に使用
    const baseName = spreadsheet.getName();
    
    const classCode = generateDialogueClass_Internal(baseName, className, types, fieldNames);
    const fileName = `${className}.cs`;
    
    saveToFolder_Internal(fileName, classCode);
    console.log(`✅ クラスファイル生成完了: ${fileName}`);
    
  } catch (error) {
    console.error(`❌ エラー: ${error.message}`);
    console.error(error.stack);
  }
}

function generateDialogueClass_Internal(baseName, fileName, types, fieldNames) {
  const className = `${baseName}DataScriptable`;
  const itemClass = `${baseName}Item`;
  const propertyName = `${baseName.toLowerCase()}Items`;
  
  let code = `using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ${baseName}データ用のScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "${baseName}Data", menuName = "Scriptable/${baseName}Data")]
public class ${className} : ScriptableObject
{
    public List<${itemClass}> ${propertyName} = new List<${itemClass}>();
}

/// <summary>
/// ${baseName}アイテムのデータクラス
/// </summary>
[System.Serializable]
public class ${itemClass}
{`;

  for (let i = 0; i < types.length && i < fieldNames.length; i++) {
    if (types[i] && fieldNames[i]) {
      const unityType = convertToUnityType_Internal(types[i]);
      code += `\n    public ${unityType} ${fieldNames[i]};`;
    }
  }

  code += `\n\n}`;
  return code;
}

function convertToUnityType_Internal(type) {
  const typeMap = {
    'string': 'string',
    'integer': 'int',  // Integerも対応
    'int': 'int',
    'float': 'float', 
    'bool': 'bool',
    'vector3': 'Vector3'
  };
  return typeMap[type.toLowerCase()] || 'string';
}