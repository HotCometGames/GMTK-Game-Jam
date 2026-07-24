import type { Metadata } from "next";
import "@fontsource-variable/caveat";
import "./globals.css";

export const metadata: Metadata = {
  title: "Gravity Is Free — A Jump-Budget Puzzle",
  description:
    "A three-round platform puzzle where every jump counts and gravity costs nothing.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
