# Photoalbum importer

## General

The importer is responsible for generating an Elasticsearch index that is used by the album website. 

Standard tasks of the importer are:

- generating captions for the photo's
- generating OCR data for the photo's
- extracting faces and recognizing faces

But of course these tasks can be extended if you have useful data to include in your album.

Because many steps in this importer are really time consuming, the output of such a step is saved in an Elasticsearch index and later all indexes are consolidated in 1 final album-index that is accessed by the website.

To be able to find related data, the index keys (ids) need to be the same for all databases. As an exception its worth mentioning the faces database. Because 1 photo can have many faces, a face will be keyed like `<id_of_the_photo>~<serial_num>`.

The import driver has the notion of a full or incremental import. In case of a full import, everything is re-computed/re-built from scratch. An incremental import will take the existing data and updates for the changes.



## Indexes and aliases

All indexes used have a base name and a timestamp. The current index has an alias that is the base name. When doing a full import, a new index is created and all data is re-imported. After a successful full import, the alias is swapped to the newly create index and old generations of the index are removed. How many generations are kept is specified in the import.xml.

 Some steps store data in storages. If that is the case, these storages follow the same procedure (base name plus timestamp) and are hard-coupled to the index name by their timestamp. When an index generation is removed, the coupled storages are removed as well.

If the website needs a storage (faces), the storage with the same timestamp as the current index will be used.



## Install & getting started

The import-driver can be downloaded from [https://bitmanager.nl/distrib/importer.zip](https://bitmanager.nl/distrib/importer.zip).

**Install** elasticsearch.
If you want to use the bitmanager stemmer, please install version 7.4, since the bitmanager plugin is not yet available for later versions.
The needed plugin is downloadable from [](https://bitmanager.nl/es/7.4.0/).
If you choose to use a different version, the standard `index.config.js` will not work. Take a look at the `index_without_stemmer.config.js` file.

**Create** a following files by copying their *.sample counterparts and adjust the content according your needs:

- import.xml
- facenames.txt
- hypernyms.txt

The best way to get started is to run a fullimport for the ID's and then run a fullimport for the photo's. It will not take a long time and you get an immediate idea of how album names are generated and whether there are problems in the sorting dates. There will be no captions, faces or OCR data.

In case of any problems, make use of importsettings.xml to control the album-name and date generation. This is especially useful for scanned images.

Maybe you can remove duplicates from your collection, rename things, put photo's together in a directory, etc. After that, you can redo these initial steps.

**Finally** start the other steps one-by-one. This will take a long time if you have lots of photo's. With the sleep_after_extract in place, and 30K photo's, the generation of captions will exceed 24 hours. OCR is slightly faster and face extraction will take hours.

#### Face stamping

After generating all the indexes, start the 'faces' page in the website and start stamping known faces. Typically stamp +-5 faces per person, and run the the match step. Then restart the faces page and correct failures. By searching for 'name:"some name"', you can see all the faces that were assigned to this person. Manual assigned faces can be seen by querying for 'src:M'. The ID of a photo and names, etc is put in the txt field and is searchable from the UI.

#### Regenerating the main index

Now rerun a full import of the photo's. The generated data will be mixed together with the meta info from the photo.



## Generating the IDs

The 1st step is to generate the IDs for the photo's (and so for the other indexes as well). An ID is basically the filename, but without the root. This makes it possible to translate an ID to different actual locations, depending on the machine where it runs.

The import also supports multiple users, so the we can have different albums for different users. 

The generated ID's and user information is stored in the "album-ids" index.

This step is not time consuming and must always be ran as a full import.



## Generating OCR data

OCR-ing is done by the Tesseract engine. Make sure that the needed tesseract-dll's can be found from the bin-dir. 

The data is simply extracted and added to the ocr index, which is "album-ocr".

The OCR config is read from the tessdata directory. Other configs can be downloaded from [](https://github.com/tesseract-ocr/tessdata) .



## Generating captions

Captions are generated by using a neural network downloaded from Huggingface: [](https://huggingface.co/microsoft/git-base-coco). The network will be accessed via a python webservice. To make sure that everything works, like all models are present in python, process one photo via the test.py and check. The captions are generated in English, but afterwards translated into Dutch via the free version of Google Translate. One problem of the free version is that it sometimes stops working. In that case restart the caption generation process. You might make sure that the alias is switched before doing so, otherwise you already generated captions will be gone...

The generation of captions is a lengthy process that easily cost more than a day.



## Extracting face data

Facerecognition is done via FaceAiSharp, which uses a network from arcface. 

If you get following error:

```Unable to find an entry point named 'OrtGetApiBase' in DLL 'onnxruntime'
Unable to find an entry point named 'OrtGetApiBase' in DLL 'onnxruntime'
```

You need to copy the contents of runtimes\win-x64 into the directory where albumimporter.dll resides. Typically 2 levels up.



The tool extracts face locations and per face it generates a vector of 512 floats. The face images and face vectors are stored in storage files, resp "album-faces_timestamp.stor" and "album-embeddings_timestamp.stor". The embeddings are used to match faces in the next step, the face images are used by the album website to show individual faces and optional to manual map them to a known person.

Known persons are administrated in the file facenames.txt. The ID of a face is the line number of the name in this file. This makes it impossible to insert new names. Always **append** new names to this text file. 

The generated face data is stored in the album-faces index. The ID of a face record is the ID of the photo, appended by ~<serial num>.

Via the website (relative url "faces"), it is possible to stamp a known name onto a face. If you do that, the face record is modified to reflect that this is a manual assigned face. You can find this in the field 'src'.

Currently following values can be found in src:

- M to indicate a manual assigned face name.
- U to indicate a not yet assigned face name.
- A to indicate an automatisc assigned face name.
- E to indicate that this is an unknown person and no name should be assigned

Extracting faces is a heavy process and will make the computer unresponsive and overheated. To cope with that the importer can do a sleep after every extract. Of course, the downside is that the process takes longer, but you might be able to still work on the computer while the extraction phase is running. The sleep is controlled via the sleep_after_extract value on the datasource.



## Matching faces

Faces are matched by comparing the embeddings from the previous step. This is a brute force compare of all the vectors, trying to map them on a known vector (associated with a src="M" record).

Its not needed to be more smart than brute force: the matching phase doesn't take that long.

The matched facename can be used later when we search for a name like name:"peter van der weerd" in the website.



## Tracks

Especially older photo's or photo's from a non-phone camera will not contain location information. Since I kept track of my walks and bycicle tours in a GPS track, its is possible to detect the actual location of the photo by matching the timestamp to the time in the gps-track. The assigned locations are stored in an index album-tracks.

The locations can be used later when we search for a location like "loc:amsterdam" in the website.



## Importing the photo's themselves

Importing photo's is done by extracting meta information about the photo. Some information obviously comes from the previous steps, other information is extracted from the EXIF-data or from the file- and directory-name.

Information extracted from EXIF:

- Date taken
- Camera
- GPS location
- Orientation and with and height

Photo's get automatically assigned to an album (and year). In the website this album is used to facet the photo's.
The album name is generated from the filename, where we ignore timestamps and serial numbers from the name.

#### Timestamps

Timestamps are typically

- 4 chars: a year
- 6 chars  a time (if separated by ':') or a year plus month.
- 8 chars a full date YYYYMMDD 

However, the date from EXIF always takes precedence.

#### Hiding photos

There are 2 hiding modes:

- only show hidden photo's when accessed from the LAN
  Photo's are hidden if the filename part start with or ends with an underscore.
  These photo's are only visible from with the LAN (or of you are authenticated)
- only show hidden photo's when accessed from the LAN and explicitly asked for.
  Photo's can be hidden by setting this in the importsettings in a directory. If that is the case, the photo's are only shown when the url of the website contains hide=false and the website is accessed from the local LAN.



#### Controlling meta information via importsettings.xml

If a directory contains the file importsettings.xml, the meta extraction is done as specified in the file.

As an example:

```
<root inherit="Allow|True|False" skip_dir_names="0" hide="None|External|Always" user="" file_order="false|true" >
   <album force="" take_album_from_dir="false|true" take_date_from_dir="false|true" minlen="0"/>
   <date  force="" type="unspecified|local|utc"/>
   <camera force="scanner" />
</root>
```

**inherit** values (flags can be combined):

- Allow: allow next level directories to use these settings too
- False: inherit from defaults and not from the parent directory
- True: inherit from the parent-directory or from defaults if no parent.



**Hide** values:

- None (default): not hidden
- External: only visible for local IPs 
- Always: only visible if accessed via local IP and unhide=true is specified at the url

**skip_dir_names**: if > 0, this amout of dirnames will be skipped while interpreting the album or date from the dirname

**file_order**: if true, generate a serialnumber that is added to the internal sort-key.

**minlen**: minimum length for an album name. If the name is smaller it is ignored 



