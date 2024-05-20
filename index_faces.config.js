{
   "settings": {
      "number_of_shards" : 4,
      "number_of_replicas" : 0,
      "refresh_interval": "5s",
      "analysis" : {
         "char_filter" : {
            "prepare_delimiter": {
               "type": "mapping",
               "mappings": [
                  ".=>\\u0020",
                  "-=>\\u0020",
                  "_=>\\u0020",
                  "\\uFFFD=>\\u0020",
                  "\\u005C=>\\u0020"
               ]
            }
         },
         "analyzer" : {
             "lc_text" : {
                "char_filter" : ["prepare_delimiter"],
                "tokenizer" : "standard",
                "filter": ["asciifolding", "lowercase"]
             }
         },
         "normalizer" : {
             "lc_facet" : {
                "filter": ["asciifolding", "lowercase"]
             }
         }
      }
   },

   "mappings": {
      "_meta": { "lastmod": "" },
      "dynamic": false,
      "properties": {
         "ts": {"type": "date"},
         "any_face": {"type": "boolean"},
         "first": {"type": "boolean"},
         "count": {"type": "integer"},
         "w0": {"type": "integer"},
         "h0": {"type": "integer"},
         "storage_id": {"type": "keyword", "doc_values": false},
         "rect": {"type": "keyword", "index": false, "doc_values": false},
         "relpos": {"type": "keyword", "index": false, "doc_values": false},
         "relpos20": {"type": "keyword", "index": false, "doc_values": false},
         "src": {"type": "keyword", "normalizer": "lc_facet"},
         "user": {"type": "keyword", "normalizer": "lc_facet"},
         "txt": {"type": "text", "analyzer": "lc_text"},
         "names": {"type": "nested", "properties": {
            "name": {"type": "text", "analyzer": "lc_text", "copy_to":["txt"], "fields": {
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