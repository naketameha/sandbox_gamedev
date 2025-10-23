#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
日本語脚本音素変換ツール ver1.0

【目的・用途】
- ゲーム用CSVファイルの脚本から音素情報を自動生成
- 列1の脚本テキストから、列2に各文字の音素数、列3に音素文字列を出力
- キャラクターの口パクアニメーション用音素データ生成
- 文字表示と連動した電子音の回数制御

【仕様】
・音素: a, i, u, e, o, n, - の7種類
・ま・ば・ぱ行（唇を閉じる音）は n+母音 で表現 (例: ま→na, ぶ→nu)
・列2: 各文字ごとの音素数を数字列で出力 (例: "吾輩は"→"221")
・列3: 音素をカンマ区切りで出力 (例: "a,a,a,i,a")
・リッチテキストタグは自動除去（Unity公式タグ+独自タグ対応）

【使用例】
入力: "吾輩は猫である。"
列2: "22122111" (吾=2音, 輩=2音, は=1音, 猫=2音, で=1音, あ=1音, る=1音, 。=1音)
列3: "a,a,a,i,a,e,o,e,u,-"

【現在の問題点・今後の改善予定】
・pykakasi使用中だが精度がイマイチ、MeCabへの切り替えを検討
・数字の読み（"20"→"にじゅう" vs "20日"→"はつか"）の文脈判定未対応
・固有名詞の読み分けが不完全
・小さい文字（ゃゅょ）の扱いが完璧ではない可能性
・UI等、アーティストに使いやすかったりUnityに渡すための諸々は、未実装

【ライブラリ変更時の注意】
・JapaneseConverterクラスを継承して新しい変換エンジンを実装
・PhonemeConverterのconverter_typeパラメータで切り替え可能
・ひらがな→音素マッピング部分は共通利用可能

【開発者】
ノベルゲーム用想定。
Windows環境、Unity使用者想定
"""

import pandas as pd
import re
from typing import List, Tuple

class JapaneseConverter:
    """
    日本語変換エンジンの基底クラス（ライブラリ差し替え用）
    
    【設計方針】
    pykakasi → MeCab などのライブラリ変更に対応するため、
    共通インターフェースを定義。新しいライブラリを使いたい場合は
    このクラスを継承して to_hiragana, to_romaji メソッドを実装する。
    """
    
    def to_hiragana(self, text: str) -> str:
        """漢字→ひらがな変換"""
        raise NotImplementedError
    
    def to_romaji(self, text: str) -> str:
        """ひらがな→ローマ字変換"""
        raise NotImplementedError

class PyKakasiConverter(JapaneseConverter):
    """
    pykakasi実装
    
    【現在の問題】
    ・精度がやや低い（特に固有名詞、数字の文脈読み）
    ・"何"→"nani"のように余計な文字が入ることがある
    ・MeCabの方が高精度だが環境構築が面倒
    
    【利点】
    ・インストールが簡単（pip install pykakasi のみ）
    ・Windows対応が確実
    ・軽量
    """
    
    def __init__(self):
        try:
            from pykakasi import kakasi
            
            # ひらがな変換用
            self.kks_hiragana = kakasi()
            self.kks_hiragana.setMode('J', 'H')  # 漢字→ひらがな
            self.kks_hiragana.setMode('K', 'H')  # カタカナ→ひらがな
            self.conv_hiragana = self.kks_hiragana.getConverter()
            
            # ローマ字変換用
            self.kks_romaji = kakasi()
            self.kks_romaji.setMode('H', 'a')    # ひらがな→ローマ字
            self.kks_romaji.setMode('K', 'a')    # カタカナ→ローマ字
            self.kks_romaji.setMode('J', 'a')    # 漢字→ローマ字
            self.conv_romaji = self.kks_romaji.getConverter()
            
        except ImportError:
            print("エラー: pykakasiがインストールされていません")
            print("pip install pykakasi を実行してください")
            raise
    
    def to_hiragana(self, text: str) -> str:
        """漢字→ひらがな変換"""
        return self.conv_hiragana.do(text)
    
    def to_romaji(self, text: str) -> str:
        """ひらがな→ローマ字変換"""
        return self.conv_romaji.do(text)

class MeCabConverter(JapaneseConverter):
    """
    MeCab実装（将来用・高精度版）
    
    【実装予定機能】
    ・より正確な漢字読み（文脈考慮）
    ・数字の適切な読み分け（"20日"→"はつか"など）
    ・固有名詞の読み精度向上
    
    【実装時の注意】
    ・Windows環境でのMeCab本体インストールが必要
    ・辞書ファイルの管理
    ・mecab-python3 または fugashi の選択
    """
    
    def __init__(self):
        # TODO: MeCab実装時にここを書く
        print("MeCab実装は未対応です")
        raise NotImplementedError
    
    def to_hiragana(self, text: str) -> str:
        # TODO: MeCab実装
        pass
    
    def to_romaji(self, text: str) -> str:
        # TODO: MeCab実装  
        pass

class PhonemeConverter:
    """音素変換メインクラス"""
    
    def __init__(self, converter_type: str = "pykakasi"):
        """
        初期化
        converter_type: "pykakasi" または "mecab"
        """
        if converter_type == "pykakasi":
            self.converter = PyKakasiConverter()
        elif converter_type == "mecab":
            self.converter = MeCabConverter()
        else:
            raise ValueError("converter_typeは 'pykakasi' か 'mecab' を指定してください")
        
        # ひらがな→音素の直接マッピング
        # 【音素仕様】aiueon- の7種類
        # ・基本音素: a,i,u,e,o（母音ベース）
        # ・n: 「ん」および唇を閉じる音（ま・ば・ぱ行の前置音）
        # ・-: 無音、句読点、記号、促音、長音
        # 【ま・ば・ぱ行の特殊処理】
        # 唇を閉じる動作を表現するため、n+母音の組み合わせで出力
        # 例: ま→na, び→ni, ぷ→nu, べ→ne, ぼ→no
        self.hiragana_phoneme_map = {
            # あ行
            'あ': 'a', 'い': 'i', 'う': 'u', 'え': 'e', 'お': 'o',
            # か行
            'か': 'a', 'き': 'i', 'く': 'u', 'け': 'e', 'こ': 'o',
            # が行
            'が': 'a', 'ぎ': 'i', 'ぐ': 'u', 'げ': 'e', 'ご': 'o',
            # さ行
            'さ': 'a', 'し': 'i', 'す': 'u', 'せ': 'e', 'そ': 'o',
            # ざ行
            'ざ': 'a', 'じ': 'i', 'ず': 'u', 'ぜ': 'e', 'ぞ': 'o',
            # た行
            'た': 'a', 'ち': 'i', 'つ': 'u', 'て': 'e', 'と': 'o',
            # だ行
            'だ': 'a', 'ぢ': 'i', 'づ': 'u', 'で': 'e', 'ど': 'o',
            # な行
            'な': 'a', 'に': 'i', 'ぬ': 'u', 'ね': 'e', 'の': 'o',
            # は行
            'は': 'a', 'ひ': 'i', 'ふ': 'u', 'へ': 'e', 'ほ': 'o',
            # ば行（唇閉じる系）
            'ば': 'na', 'び': 'ni', 'ぶ': 'nu', 'べ': 'ne', 'ぼ': 'no',
            # ぱ行（唇閉じる系）
            'ぱ': 'na', 'ぴ': 'ni', 'ぷ': 'nu', 'ぺ': 'ne', 'ぽ': 'no',
            # ま行（唇閉じる系）
            'ま': 'na', 'み': 'ni', 'む': 'nu', 'め': 'ne', 'も': 'no',
            # や行
            'や': 'a', 'ゆ': 'u', 'よ': 'o',
            # ら行
            'ら': 'a', 'り': 'i', 'る': 'u', 'れ': 'e', 'ろ': 'o',
            # わ行
            'わ': 'a', 'ゐ': 'i', 'ゑ': 'e', 'を': 'o',
            # ん
            'ん': 'n',
            # 小さい文字
            'ゃ': 'a', 'ゅ': 'u', 'ょ': 'o',
            'っ': '-',  # 促音
            'ー': '-',  # 長音
        }
        
        # ま・ば・ぱ行の特殊処理用（使わなくなったが念のため残す）
        self.special_consonants = {
            'ma': 'na', 'mi': 'ni', 'mu': 'nu', 'me': 'ne', 'mo': 'no',
            'ba': 'na', 'bi': 'ni', 'bu': 'nu', 'be': 'ne', 'bo': 'no', 
            'pa': 'na', 'pi': 'ni', 'pu': 'nu', 'pe': 'ne', 'po': 'no'
        }
    
    def romaji_to_phoneme(self, romaji: str) -> List[str]:
        """ローマ字を音素リストに変換"""
        phonemes = []
        i = 0
        
        while i < len(romaji):
            # 2文字の組み合わせをチェック（ma, ba, pa系）
            if i + 1 < len(romaji):
                two_char = romaji[i:i+2]
                if two_char in self.special_consonants:
                    phonemes.append(self.special_consonants[two_char])
                    i += 2
                    continue
            
            # 1文字ずつ処理
            char = romaji[i]
            if char in 'aiueo':
                phonemes.append(char)
            elif char == 'n':
                phonemes.append('n')
            elif char in ' \t\n.,。、-':
                phonemes.append('-')
            # その他の子音は無視
            
            i += 1
        
        return phonemes
    
    def char_to_phoneme(self, char: str) -> str:
        """1文字を音素に変換"""
        if char in 'aiueo':
            return char
        elif char == 'n':
            return 'n'
        elif char in ' \t\n.,。、':
            return '-'
        else:
            return None  # スキップ
    
    def count_mora(self, hiragana: str) -> int:
        """文字数（モーラ数）をカウント"""
        # 小さい文字（ゃゅょっ）を考慮した文字数計算
        small_chars = 'ゃゅょっァィゥェォャュョッ'
        count = 0
        
        for char in hiragana:
            if char not in small_chars and char not in ' \t\n.,。、':
                count += 1
        
        return count
    
    def hiragana_to_phoneme(self, hiragana_char: str) -> str:
        """ひらがな1文字を音素に変換"""
        return self.hiragana_phoneme_map.get(hiragana_char, '-')
    
    def clean_rich_text(self, text: str) -> str:
        """リッチテキストタグを除去"""
        # Unity公式サポートタグを除去
        # https://docs.unity3d.com/ja/2022.3/Manual/UIE-supported-tags.html
        
        # 開始・終了タグのペア
        text = re.sub(r'<b>|</b>', '', text)           # 太字
        text = re.sub(r'<i>|</i>', '', text)           # 斜体
        text = re.sub(r'<u>|</u>', '', text)           # 下線
        text = re.sub(r'<s>|</s>', '', text)           # 取り消し線
        text = re.sub(r'<sub>|</sub>', '', text)       # 下付き文字
        text = re.sub(r'<sup>|</sup>', '', text)       # 上付き文字
        text = re.sub(r'<mark>|</mark>', '', text)     # ハイライト
        text = re.sub(r'<small>|</small>', '', text)   # 小さい文字
        text = re.sub(r'<nobr>|</nobr>', '', text)     # 改行なし
        
        # パラメータ付きタグ
        text = re.sub(r'<color=[^>]*>|</color>', '', text)           # 色
        text = re.sub(r'<size=[^>]*>|</size>', '', text)             # サイズ
        text = re.sub(r'<font=[^>]*>|</font>', '', text)             # フォント
        text = re.sub(r'<material=[^>]*>|</material>', '', text)     # マテリアル
        text = re.sub(r'<quad=[^>]*>', '', text)                     # クアッド（単体タグ）
        text = re.sub(r'<sprite=[^>]*>', '', text)                   # スプライト（単体タグ）
        text = re.sub(r'<space=[^>]*>', '', text)                    # スペース（単体タグ）
        text = re.sub(r'<style=[^>]*>|</style>', '', text)           # スタイル
        text = re.sub(r'<align=[^>]*>|</align>', '', text)           # 整列
        text = re.sub(r'<alpha=[^>]*>|</alpha>', '', text)           # 透明度
        text = re.sub(r'<cspace=[^>]*>|</cspace>', '', text)         # 文字間隔
        text = re.sub(r'<font-weight=[^>]*>|</font-weight>', '', text) # フォント太さ
        text = re.sub(r'<indent=[^>]*>|</indent>', '', text)         # インデント
        text = re.sub(r'<line-height=[^>]*>|</line-height>', '', text) # 行の高さ
        text = re.sub(r'<line-indent=[^>]*>|</line-indent>', '', text) # 行インデント
        text = re.sub(r'<link=[^>]*>|</link>', '', text)             # リンク
        text = re.sub(r'<lowercase>|</lowercase>', '', text)         # 小文字
        text = re.sub(r'<uppercase>|</uppercase>', '', text)         # 大文字
        text = re.sub(r'<smallcaps>|</smallcaps>', '', text)         # スモールキャップ
        text = re.sub(r'<margin=[^>]*>|</margin>', '', text)         # マージン
        text = re.sub(r'<monospace=[^>]*>|</monospace>', '', text)   # 等幅フォント
        text = re.sub(r'<mspace=[^>]*>', '', text)                   # 等幅スペース（単体タグ）
        text = re.sub(r'<noparse>|</noparse>', '', text)             # パース無効
        text = re.sub(r'<page>', '', text)                           # ページ区切り（単体タグ）
        text = re.sub(r'<pos=[^>]*>', '', text)                      # 位置（単体タグ）
        text = re.sub(r'<rotate=[^>]*>|</rotate>', '', text)         # 回転
        text = re.sub(r'<strikethrough>|</strikethrough>', '', text) # 取り消し線
        text = re.sub(r'<underline>|</underline>', '', text)         # 下線
        text = re.sub(r'<voffset=[^>]*>|</voffset>', '', text)       # 垂直オフセット
        text = re.sub(r'<width=[^>]*>|</width>', '', text)           # 幅
        
        # 独自タグ（今後使いたくなるかもしれないので存在しないタグも一応入れてます）
        text = re.sub(r'\[shake\]|\[/shake\]', '', text)             # シェイク効果
        text = re.sub(r'\[bounce\]|\[/bounce\]', '', text)           # バウンス効果
        text = re.sub(r'\[fade\]|\[/fade\]', '', text)               # フェード効果
        text = re.sub(r'\[voice=[^\]]*\]|\[/voice\]', '', text)      # 音声指定
        text = re.sub(r'\[speed=[^\]]*\]|\[/speed\]', '', text)      # 表示速度指定
        text = re.sub(r'\[wait=[^\]]*\]', '', text)                  # 待機指定（単体タグ）
        text = re.sub(r'\[emotion=[^\]]*\]|\[/emotion\]', '', text)  # 感情表現
        
        return text
    
    def process_text(self, text: str) -> Tuple[str, str]:
        """
        テキストを処理して各文字の音素数と音素文字列を返す
        Returns: (音素数文字列, 音素文字列)  例: ("211", "a,i,e,o")
        """
        # リッチテキストタグを除去
        text = self.clean_rich_text(text)
        
        # 改行や空白は除去、句読点・記号は残す
        clean_text = re.sub(r'[\r\n\s]', '', text)
        
        if not clean_text:
            return "0", "-"
        
        phoneme_counts = []
        all_phonemes = []
        
        # 1文字ずつ処理
        for char in clean_text:
            # 句読点・記号の処理
            if char in '.,。、！？!?；;：:「」『』()（）[]【】〈〉《》""''…‥・':
                phoneme_counts.append("1")
                all_phonemes.append("-")
                continue
            
            # 漢字→ひらがな変換
            char_hiragana = self.converter.to_hiragana(char)
            
            # ひらがな1文字ずつ音素変換
            char_phonemes = []
            for hiragana_char in char_hiragana:
                phoneme = self.hiragana_to_phoneme(hiragana_char)
                if phoneme != '-':
                    char_phonemes.append(phoneme)
            
            # この文字の音素数をカウント
            phoneme_count = len(char_phonemes) if char_phonemes else 1
            phoneme_counts.append(str(phoneme_count))
            
            # 音素リストに追加
            if char_phonemes:
                all_phonemes.extend(char_phonemes)
            else:
                all_phonemes.append('-')
        
        # 結果を文字列として結合
        count_str = ''.join(phoneme_counts)
        phoneme_str = ','.join(all_phonemes) if all_phonemes else '-'
        
        return count_str, phoneme_str

def process_csv(input_file: str, output_file: str, converter_type: str = "pykakasi"):
    """
    CSVファイルを処理して音素情報を追加
    
    Args:
        input_file: 入力CSVファイルパス
        output_file: 出力CSVファイルパス  
        converter_type: 変換エンジン ("pykakasi" or "mecab")
    """
    try:
        # CSV読み込み
        df = pd.read_csv(input_file, encoding='utf-8')
        
        # 音素変換器初期化
        converter = PhonemeConverter(converter_type)
        
        # 列1（脚本）を処理
        script_column = df.iloc[:, 0]  # 1列目
        
        phoneme_count_strings = []
        phoneme_strings = []
        
        for text in script_column:
            if pd.isna(text):
                phoneme_count_strings.append("0")
                phoneme_strings.append('-')
            else:
                count_str, phonemes = converter.process_text(str(text))
                phoneme_count_strings.append(count_str)
                phoneme_strings.append(phonemes)
        
        # 列2に各文字の音素数、列3に音素情報を設定
        df.iloc[:, 1] = phoneme_count_strings
        df.iloc[:, 2] = phoneme_strings
        
        # CSV出力
        df.to_csv(output_file, index=False, encoding='utf-8')
        
        print(f"処理完了: {input_file} → {output_file}")
        print(f"変換エンジン: {converter_type}")
        
    except Exception as e:
        print(f"エラー: {e}")

if __name__ == "__main__":
    # テスト用
    input_file = "入力.csv"
    output_file = "出力_test.csv"
    
    # pykakasi使用
    process_csv(input_file, output_file, "pykakasi")
    
    # 将来MeCabに変更したい場合：
    # process_csv(input_file, output_file, "mecab")