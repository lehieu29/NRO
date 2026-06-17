using System;
using Assets.src.e;

public class SmallImage
{
	public static int[][] smallImg;

	public static SmallImage instance;

	public static Image[] imgbig;

	public static Small[] imgNew;

	public static MyVector vKeys = new MyVector();

	public static Image imgEmpty = null;

	public static sbyte[] newSmallVersion;

	public static MyVector vt_images_watingDowload = new MyVector();

	public static int smallCount;

	public static short maxSmall;

	public SmallImage()
	{
		readImage();
	}

	public static void loadBigRMS()
	{
		if (imgbig == null)
		{
			imgbig = new Image[5]
			{
				GameCanvas.loadImageRMS("/img/Big0.png"),
				GameCanvas.loadImageRMS("/img/Big1.png"),
				GameCanvas.loadImageRMS("/img/Big2.png"),
				GameCanvas.loadImageRMS("/img/Big3.png"),
				GameCanvas.loadImageRMS("/img/Big4.png")
			};
		}
	}

	public static void freeBig()
	{
		imgbig = null;
		mSystem.gcc();
	}

	public static void loadBigImage()
	{
		imgEmpty = Image.createRGBImage(new int[1], 1, 1, bl: true);
	}

	public static void init()
	{
		instance = null;
		instance = new SmallImage();
	}

	public void readData(byte[] data)
	{
	}

	public void readImage()
	{
		int num = 0;
		try
		{
			DataInputStream dataInputStream = new DataInputStream(Rms.loadRMS("NR_image"));
			short num2 = dataInputStream.readShort();
			smallImg = new int[num2][];
			for (int i = 0; i < smallImg.Length; i++)
			{
				smallImg[i] = new int[5];
			}
			for (int j = 0; j < num2; j++)
			{
				num++;
				smallImg[j][0] = dataInputStream.readUnsignedByte();
				smallImg[j][1] = dataInputStream.readShort();
				smallImg[j][2] = dataInputStream.readShort();
				smallImg[j][3] = dataInputStream.readShort();
				smallImg[j][4] = dataInputStream.readShort();
			}
		}
		catch (Exception ex)
		{
			Cout.LogError3("Loi readImage: " + ex.ToString() + "i= " + num);
		}
	}

	public static void clearHastable()
	{
	}

	public static void createImage(int id)
	{
		Res.outz("is request =" + id + " zoom=" + mGraphics.zoomLevel);
		if (HsnrConfig.useHsnrProtocol)
		{
			// HSNR: bundled DragonBoy250 /SmallImage assets do not match the server's
			// part ids and decode to junk 1x1/4x4 textures. Match the native client:
			// only use a local/cached image if it is real (>4x4); otherwise request it
			// from the server via cmd=-67 (like native createImage on a cache miss).
			Image local = null;
			try
			{
				local = GameCanvas.loadImage("/SmallImage/Small" + id + ".png");
			}
			catch (Exception)
			{
			}
			if (local != null && mGraphics.getImageWidth(local) > 4 && mGraphics.getImageHeight(local) > 4)
			{
				imgNew[id] = new Small(local, id);
				HsnrLog.Log("IMG", "createImage LOCAL id=" + id + " " + mGraphics.getImageWidth(local) + "x" + mGraphics.getImageHeight(local));
				return;
			}
			sbyte[] cached = Rms.loadRMS(mGraphics.zoomLevel + "Small" + id);
			if (cached != null)
			{
				// HSNR cache stores the server-format (byte-reversed) PNG; reverse to decode.
				sbyte[] cpng = new sbyte[cached.Length];
				for (int r = 0; r < cached.Length; r++)
				{
					cpng[r] = cached[cached.Length - 1 - r];
				}
				Image im = Image.createImage(cpng, 0, cpng.Length);
				if (im != null && mGraphics.getImageWidth(im) > 4 && mGraphics.getImageHeight(im) > 4)
				{
					imgNew[id] = new Small(im, id);
					HsnrLog.Log("IMG", "createImage RMS id=" + id + " " + mGraphics.getImageWidth(im) + "x" + mGraphics.getImageHeight(im));
					return;
				}
			}
			imgNew[id] = new Small(imgEmpty, id);
			HsnrLog.Log("IMG", "createImage REQ -67 id=" + id + " localW=" + ((local == null) ? -1 : mGraphics.getImageWidth(local)) + " cachedLen=" + ((cached == null) ? -1 : cached.Length));
			Service.gI().requestIcon(id);
			return;
		}
		if (mGraphics.zoomLevel == 1)
		{
			Image image = GameCanvas.loadImage("/SmallImage/Small" + id + ".png");
			if (image != null)
			{
				imgNew[id] = new Small(image, id);
				return;
			}
			imgNew[id] = new Small(imgEmpty, id);
			if (GameCanvas.currentScreen == GameCanvas._SelectCharScr)
			{
				Service.gI().requestIcon(id);
			}
			else
			{
				if (HsnrConfig.useHsnrProtocol)
				{
					HsnrLog.Log("IMG", "createImage MISS id=" + id + " -> queue (zoom1), queueSize=" + (vt_images_watingDowload.size() + 1) + " scr=" + (GameCanvas.currentScreen == GameScr.gI()) + " isReqMap=" + GameCanvas.isRequestMapID);
				}
				vt_images_watingDowload.addElement(imgNew[id]);
			}
			return;
		}
		Image image2 = GameCanvas.loadImage("/SmallImage/Small" + id + ".png");
		if (image2 != null)
		{
			imgNew[id] = new Small(image2, id);
			return;
		}
		bool flag = false;
		sbyte[] array = Rms.loadRMS(mGraphics.zoomLevel + "Small" + id);
		if (array != null)
		{
			if (newSmallVersion != null && array.Length % 127 != newSmallVersion[id])
			{
				flag = true;
			}
			if (!flag)
			{
				Image image3 = Image.createImage(array, 0, array.Length);
				if (image3 != null)
				{
					imgNew[id] = new Small(image3, id);
				}
				else
				{
					flag = true;
				}
			}
		}
		else
		{
			flag = true;
		}
		if (flag)
		{
			imgNew[id] = new Small(imgEmpty, id);
			if (GameCanvas.currentScreen == GameCanvas._SelectCharScr)
			{
				Service.gI().requestIcon(id);
			}
			else
			{
				if (HsnrConfig.useHsnrProtocol)
				{
					HsnrLog.Log("IMG", "createImage MISS id=" + id + " -> queue (zoom" + mGraphics.zoomLevel + "), queueSize=" + (vt_images_watingDowload.size() + 1) + " scr=" + (GameCanvas.currentScreen == GameScr.gI()) + " isReqMap=" + GameCanvas.isRequestMapID);
				}
				vt_images_watingDowload.addElement(imgNew[id]);
			}
		}
	}

	public static void drawSmallImage(mGraphics g, int id, int x, int y, int transform, int anchor)
	{
		if (imgbig == null)
		{
			Small small = imgNew[id];
			if (small == null)
			{
				createImage(id);
			}
			else
			{
				g.drawRegion(small, 0, 0, mGraphics.getImageWidth(small.img), mGraphics.getImageHeight(small.img), transform, x, y, anchor);
			}
		}
		else if (smallImg != null)
		{
			if (id >= smallImg.Length || smallImg[id][1] >= 256 || smallImg[id][3] >= 256 || smallImg[id][2] >= 256 || smallImg[id][4] >= 256)
			{
				Small small2 = imgNew[id];
				if (small2 == null)
				{
					createImage(id);
				}
				else
				{
					small2.paint(g, transform, x, y, anchor);
				}
			}
			else if (imgbig[smallImg[id][0]] != null)
			{
				g.drawRegion(imgbig[smallImg[id][0]], smallImg[id][1], smallImg[id][2], smallImg[id][3], smallImg[id][4], transform, x, y, anchor);
			}
		}
		else if (GameCanvas.currentScreen != GameScr.gI())
		{
			Small small3 = imgNew[id];
			if (small3 == null)
			{
				createImage(id);
			}
			else
			{
				small3.paint(g, transform, x, y, anchor);
			}
		}
	}

	public static void drawSmallImage(mGraphics g, int id, int f, int x, int y, int w, int h, int transform, int anchor)
	{
		if (imgbig == null)
		{
			Small small = imgNew[id];
			if (small == null)
			{
				createImage(id);
			}
			else
			{
				g.drawRegion(small.img, 0, f * w, w, h, transform, x, y, anchor);
			}
		}
		else if (smallImg != null)
		{
			if (id >= smallImg.Length || smallImg[id] == null || smallImg[id][1] >= 256 || smallImg[id][3] >= 256 || smallImg[id][2] >= 256 || smallImg[id][4] >= 256)
			{
				Small small2 = imgNew[id];
				if (small2 == null)
				{
					createImage(id);
				}
				else
				{
					small2.paint(g, transform, f, x, y, w, h, anchor);
				}
			}
			else if (smallImg[id][0] != 4 && imgbig[smallImg[id][0]] != null)
			{
				g.drawRegion(imgbig[smallImg[id][0]], 0, f * w, w, h, transform, x, y, anchor);
			}
			else
			{
				Small small3 = imgNew[id];
				if (small3 == null)
				{
					createImage(id);
				}
				else
				{
					small3.paint(g, transform, f, x, y, w, h, anchor);
				}
			}
		}
		else if (GameCanvas.currentScreen != GameScr.gI())
		{
			Small small4 = imgNew[id];
			if (small4 == null)
			{
				createImage(id);
			}
			else
			{
				small4.paint(g, transform, f, x, y, w, h, anchor);
			}
		}
	}

	public static void update()
	{
		int num = 0;
		if (GameCanvas.gameTick % 1000 != 0)
		{
			return;
		}
		for (int i = 0; i < imgNew.Length; i++)
		{
			if (imgNew[i] != null)
			{
				num++;
				imgNew[i].update();
				smallCount++;
			}
		}
		if (num > 200 && GameCanvas.lowGraphic)
		{
			imgNew = new Small[maxSmall];
		}
	}
}
