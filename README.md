# Analyzing-and-Extracting-information-from-DICOM-Image-Files

DICOM – Digital Imaging and Communications in Medicine is a format which contains an image from a medical scan, like ultrasound or MRI. They are used to store, exchange and transmit medical images enabling the integration of medical imaging devices like scanners, servers, workstations, etc. from multiple manufacturers). The goal of the console application is to analyze and extract information from DICOM files.

The database schema was updated with a new field 'ExpNumInstances' to store the database the DICOM tag value. The value from the DICOM tags was extracted for specific groups and elements and added to the newly created field in the database.

It's value  was compared with the existing value calculated mathematically and further processing was performed depending on a match or no-match. A functionality to convert into unisigned short was also added.
