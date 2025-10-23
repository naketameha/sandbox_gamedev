// メニューを作成する関数
function onOpen() {
  var ui = SpreadsheetApp.getUi();
  ui.createMenu('ID管理')
    .addItem('新規IDを生成', 'generateUniqueIds')
    .addToUi();
}

// ID生成の関数
function generateUniqueIds() {
  var sheet = SpreadsheetApp.getActiveSheet();
  var lastRow = sheet.getLastRow();
  if (lastRow < 2) return;
  
  // データ範囲全体を取得（A〜E列くらいまで）
  var dataRange = sheet.getRange(2, 1, lastRow-1, 5);
  var data = dataRange.getValues();
  
  // C列の既存ID一覧を作成（インデックス2がC列）
  var existingIds = new Set(data.map(row => row[2]).filter(id => id !== ""));
  
  // 生成したIDをカウント
  var generatedCount = 0;
  
  // 各行をチェック
  for (var i = 0; i < data.length; i++) {
    // C列が空で、かつD列かE列にデータがある行のみ処理
    if (!data[i][2] && (data[i][3] || data[i][4])) {
      var newId;
      do {
        newId = generateId();
      } while (existingIds.has(newId));
      
      data[i][2] = newId;  // C列に書き込み
      existingIds.add(newId);
      generatedCount++;
    }
  }
  
  // 変更があった場合のみ書き戻す
  if (generatedCount > 0) {
    dataRange.setValues(data);
    SpreadsheetApp.getUi().alert(
      generatedCount + "件のIDを生成しました！\n" +
      "※D列かE列にデータがある行のみ生成"
    );
  } else {
    SpreadsheetApp.getUi().alert(
      "新規ID生成の必要はありませんでした\n" +
      "※D列かE列にデータがある行のみ生成対象"
    );
  }
}

// ID生成関数（6文字の16進数）
function generateId() {
  var id = Math.floor(Math.random() * 16777215).toString(16);
  return id.padStart(6, '0').toLowerCase();
}