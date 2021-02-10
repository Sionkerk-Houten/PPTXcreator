﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Presentation;
using Drawing = DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Packaging;

namespace PPTXcreator
{
    class PowerPoint
    {
        private PresentationDocument Document { get; }
        private PresentationPart PresPart { get => Document.PresentationPart; }
        private SlidePart[] Slides { get => PresPart.SlideParts.ToArray(); }
        public static Dictionary<string, string> Keywords { get; set; }
        
        /// <summary>
        /// Constructs a Presentation object from the .pptx file at <paramref name="openPath"/>,
        /// and sets it to save to <paramref name="savePath"/>.
        /// </summary>
        public PowerPoint(string openPath, string savePath)
        {
            PresentationDocument document = PresentationDocument.Open(openPath, true);
            Document = (PresentationDocument)document.Clone(savePath, true);
        }

        /// <summary>
        /// Replaces all keywords in the presentation with their respective values
        /// </summary>
        /// <param name="keywords">A dictionary containing replaceable strings
        /// and what they should be replaced by</param>
        public void ReplaceKeywords(Dictionary<string, string> keywords)
        {
            // Loop over slides
            foreach (SlidePart slide in Slides)
            {
                // Loop over text in slides
                foreach (Drawing.Text text in slide.Slide.Descendants<Drawing.Text>())
                {
                    StringBuilder sb = new StringBuilder(text.Text);

                    // Loop over replacable keywords
                    foreach (KeyValuePair<string, string> kvp in keywords)
                    {
                        sb.Replace(kvp.Key, kvp.Value);
                    }

                    text.Text = sb.ToString();
                }
            }
        }

        /// <summary>
        /// Replaces the image with description <see cref="Settings.QRdescription"/>
        /// with the image at <paramref name="imagePath"/>.
        /// </summary>
        public void ReplaceImage(string imagePath)
        {
            if (!File.Exists(imagePath)) return;

            // Loop over slides (not really necessary if the slide number is known)
            foreach (SlidePart slide in Slides)
            {
                // Loop over all picture objects
                foreach (Picture pic in slide.Slide.Descendants<Picture>())
                {
                    // Get the description and rId from the object
                    string description = pic.NonVisualPictureProperties.NonVisualDrawingProperties.Description;
                    string rId = pic.BlipFill.Blip.Embed.Value;
                    Console.WriteLine($"rid: {rId}, description: {description}");

                    if (description == Settings.QRdescription)
                    {
                        // Get the ImagePart by id, and replace the image
                        ImagePart imagePart = (ImagePart)slide.GetPartById(rId);
                        FileStream imageStream = File.OpenRead(imagePath);
                        imagePart.FeedData(imageStream);
                        imageStream.Close();
                    }
                }
            }
        }

        /// <summary>
        /// Copy the first slide of this document and paste it at the end,
        /// and make the copied slide visible if it was hidden
        /// </summary>
        public void DuplicateFirstSlide()
        {
            // Get the SlideIdList and the largest id in it
            SlideIdList idList = PresPart.Presentation.SlideIdList;
            uint maxId = (from slideId in idList select ((SlideId)slideId).Id).Max();

            // Get the first SlidePart from the SlideIdList
            SlideId sourceSlideId = (SlideId)idList.FirstChild;
            SlidePart sourceSlidePart = (SlidePart)PresPart.GetPartById(sourceSlideId.RelationshipId);

            // Copy the slide and SlideLayoutPart to a new slidepart, set it to visible
            SlidePart targetSlidePart = PresPart.AddNewPart<SlidePart>();
            sourceSlidePart.Slide.Save(targetSlidePart);
            targetSlidePart.AddPart(sourceSlidePart.SlideLayoutPart);
            targetSlidePart.Slide.Show = true;

            // Append a new id for the slidepart to the SlideIdList
            SlideId targetSlideId = idList.AppendChild(new SlideId());
            targetSlideId.Id = maxId + 1;
            targetSlideId.RelationshipId = PresPart.GetIdOfPart(targetSlidePart);

            PresPart.Presentation.Save();
        }

        /// <summary>
        /// Save and close the presentation
        /// </summary>
        public void SaveClose()
        {
            Document.Close();
            Document.Dispose(); // Is this necessary? Probably not but it feels appropiate
        }
    }
}
