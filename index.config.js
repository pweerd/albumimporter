{
   "settings": {
      "number_of_shards" : 1,
      "number_of_replicas" : 0,
      "refresh_interval": "60s",
      "analysis" : {
         "char_filter" : {
            "html_strip" : {
               "type" : "html_strip",
               "read_ahead" : 1024
            },
            "prepare_delimiter": {
               "type": "mapping",
               "mappings": [
                  "-=>_",
                  "\\uFFFD=>_"
               ]
            }
         },
         "filter" : {
            "stemmer_nl": {
              "type": "bm_stemmer",
              "min_length": 0,
              "language": "dutch_kp",
              "append_stem": "?",
              "emit_original": "false"
            },
            "stemmer_nl_plus": {
              "type": "bm_stemmer",
              "min_length": 0,
              "language": "dutch_kp",
              "append_stem": "?",
              "emit_original": "true"
            },
            "stemmer_en": {
              "type": "bm_stemmer",
              "min_length": 0,
              "language": "dutch_kp",
              "append_stem": "?",
              "emit_original": "false"
            },
            "stemmer_en_plus": {
              "type": "bm_stemmer",
              "min_length": 0,
              "language": "dutch_kp",
              "append_stem": "?",
              "emit_original": "true"
            },
            "index_delimiter": {
              "type": "word_delimiter_graph",
              "split_on_numerics": true,
              "catenate_words": true,
              "catenate_numbers": true,
              "preserve_original": false,
              "adjust_offsets": true
            },
            "search_delimiter": {
              "type": "word_delimiter_graph",
              "split_on_numerics": false,
              "generate_number_parts": false,
              "catenate_words": false,
              "catenate_numbers": true,
              "preserve_original": false,
              "adjust_offsets": true
            },
            "syn_repl": {
              "type": "synonym",
              "synonyms": [
                "rijkswoud=>reichswald",
                "sint=>sinterklaas",
                "parasol=>paraplu",
                "parasols=>paraplu",
                "za,zadag=>zaterdag",
                "zo,zodag=>zondag",
                "ma,madag=>maandag",
                "di,didag=>dinsdag",
                "wo,wodag=>woensdag",
                "do,dodag=>donderdag",
                "vr,vrdag=>vrijdag",
                "famdag=>familiedag",
                "jeannet=>jeannette",
                "marian=>marianne",
                "ej,erik=>erikjan",
                "beek,beekje,kanaal,rivier=>rivier",
                "tessel=>texel",
                "paddestoel=>paddenstoel",
                "paddestoelen=>paddenstoelen",
                "paardebloemen=>paardenbloemen",
                "paardebloem=>paardenbloem",
                "zonsopkomst=>zonsondergang",
                "zonsopgang=>zonsondergang",
                "hijskraan=>kraan",
                "giraffe=>giraf"
              ]
            },

            "syn_add": {
               "type": "synonym",
               "synonyms": [
               ]
            }
          },
          "analyzer" : {
             "lc_keyword" : {
                "tokenizer" : "standard",
                "filter": ["lowercase"]
             },
             "lc_text" : {
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase"]
             },
             "lc_text_index" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "syn_add", "index_delimiter"]
             },
             "en_stemmed_index" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "syn_add", "index_delimiter", "stemmer_en_plus"]
             },
             "nl_stemmed_index" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "syn_add", "index_delimiter", "stemmer_nl_plus"]
             },
             "lc_text_search" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                 "filter": ["asciifolding", "lowercase", "syn_repl", "search_delimiter"]
             },
             "en_stemmed_search" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "search_delimiter", "stemmer_en"]
             },
             "en_stemmed_search_plus" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "search_delimiter", "stemmer_en_plus"]
             },
             "nl_stemmed_search" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "search_delimiter", "stemmer_nl"]
             },
             "nl_stemmed_search_plus" : {
                "char_filter" : ["html_strip", "prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase", "syn_repl", "search_delimiter", "stemmer_nl_plus"]
             }
          },
          "normalizer" : {
            "lc_facet" : {
               "type" : "custom",
                  "filter": ["asciifolding", "lowercase"]
            }
         }

      }
   },

   "mappings": {
      "_meta": { "lastmod": "" },
      "_source": { "excludes": ["_text"] },
      "dynamic": false,
      "properties": {
         "file": { "type": "text", "analyzer": "lc_text" },
         "date": {"type": "date"},
         "year": {"type": "integer"},
         "month": {"type": "integer"},
         "day": { "type": "integer" },
         "sort_key": { "type": "long", "doc_values": true },
         "hide": { "type": "text", "analyzer": "lc_keyword" },
         "album": {
            "type": "text", "analyzer": "lc_text_index", "search_analyzer": "lc_text_search",
               "fields": {
               "facet": { "type": "keyword", "normalizer": "lc_facet", "doc_values": true }
            }
         },
         "season": { "type": "keyword", "normalizer": "lc_facet", "doc_values": false },
         "relname_offset": { "type": "integer", "doc_values": false, "index":false },
         "album_len": { "type": "integer", "doc_values": true },
         "camera": { "type": "text", "analyzer": "lc_text_index", "search_analyzer": "lc_text_search" },
         "tz": { "type": "text", "analyzer": "lc_text"},
         "location": { "type": "geo_point"},
         "ocr": { "type": "text", "analyzer": "lc_text_index", "search_analyzer": "lc_text_search", "copy_to": ["text_en", "text_nl"] },
         "text": { "type": "text", "analyzer": "lc_text_index", "search_analyzer": "lc_text_search", "copy_to": ["text_en", "text_nl"] },
         "text_en": { "type": "text", "analyzer": "en_stemmed_index", "search_analyzer": "en_stemmed_search" },
         "text_nl": { "type": "text", "analyzer": "nl_stemmed_index", "search_analyzer": "nl_stemmed_search" },
         "extra": { "type": "keyword", "normalizer": "lc_facet" },
         "root": { "type": "keyword", "normalizer": "lc_facet" },
         "user": { "type": "keyword", "normalizer": "lc_facet" },
         "yyyymmdd": { "type": "keyword" },
         "ext": { "type": "keyword", "normalizer": "lc_facet" },
         "orientation": { "type": "integer", "index": true },
         "height": { "type": "integer", "index": false },
         "width": { "type": "integer", "index": false },
         "ele": { "type": "integer", "index": false },
         "face_count": { "type": "integer" },
         "names": {"type": "nested", "properties": {
            "name": {"type": "text", "analyzer": "lc_text", "copy_to":["text_nl"], "fields": {
               "facet": { "type": "keyword", "normalizer": "lc_facet", "doc_values": true }
            }},
            "id": {"type": "integer"},
            "match_score": {"type": "float"},
            "face_detect_score": {"type": "float"},
            "detected_face_detect_score": {"type": "float"},
            "score_all": {"type": "float"}
         }}

      }
   }
}